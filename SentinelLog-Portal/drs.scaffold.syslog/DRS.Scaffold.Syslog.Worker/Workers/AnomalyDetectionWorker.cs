using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Worker.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Periodically evaluates active <see cref="AnomalyRule"/> entries against recent log traffic
/// and creates <see cref="AnomalyEvent"/> rows when thresholds are exceeded.
/// If the rule has <c>AutoCreateIncident = true</c>, an <see cref="Incident"/> is also created.
/// </summary>
public sealed class AnomalyDetectionWorker : BackgroundService
{
    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly WorkerOptions                  _opts;
    private readonly ILogger<AnomalyDetectionWorker> _logger;

    public AnomalyDetectionWorker(
        IServiceScopeFactory              scopeFactory,
        IOptions<WorkerOptions>           options,
        ILogger<AnomalyDetectionWorker>   logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AnomalyDetectionWorker starting. CheckInterval={Interval}s",
            _opts.AnomalyCheckIntervalSeconds);

        var interval = TimeSpan.FromSeconds(_opts.AnomalyCheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateRulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AnomalyDetectionWorker cycle error");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("AnomalyDetectionWorker stopped.");
    }

    private async Task EvaluateRulesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();
        var now = DateTimeOffset.UtcNow;

        var rules = await db.AnomalyRules
            .AsNoTracking()
            .Where(r => r.Enabled && !r.IsDeleted)
            .ToListAsync(ct);

        if (rules.Count == 0) return;

        foreach (var rule in rules)
        {
            try
            {
                await EvaluateRuleAsync(db, rule, now, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Anomaly rule id={Id} '{Name}' evaluation failed", rule.Id, rule.Name);
            }
        }
    }

    private async Task EvaluateRuleAsync(
        SyslogDbContext db, AnomalyRule rule, DateTimeOffset now, CancellationToken ct)
    {
        var baselineFrom  = now.AddMinutes(-rule.BaselineWindowMinutes * 2);
        var baselineUntil = now.AddMinutes(-rule.BaselineWindowMinutes);
        var observeFrom   = now.AddMinutes(-rule.BaselineWindowMinutes);

        double baseline;
        double observed;
        string? affectedEntity = null;

        switch (rule.MetricType)
        {
            case "EpsTotal":
            {
                var baselineCount = await db.LogEvents
                    .CountAsync(e => e.EventTime >= baselineFrom && e.EventTime < baselineUntil, ct);
                var observedCount = await db.LogEvents
                    .CountAsync(e => e.EventTime >= observeFrom, ct);

                var baseSecs = rule.BaselineWindowMinutes * 60.0;
                baseline = baselineCount / baseSecs;
                observed = observedCount / baseSecs;
                break;
            }

            case "SeveritySpike":
            {
                // Critical + Emergency events (severity <= 2)
                var baselineCount = await db.LogEvents
                    .CountAsync(e => e.EventTime >= baselineFrom && e.EventTime < baselineUntil
                                     && e.Severity != null && e.Severity <= 2, ct);
                var observedCount = await db.LogEvents
                    .CountAsync(e => e.EventTime >= observeFrom
                                     && e.Severity != null && e.Severity <= 2, ct);

                baseline = baselineCount;
                observed = observedCount;
                break;
            }

            case "AuthFailureRate":
            {
                var baselineCount = await db.LogEvents
                    .CountAsync(e => e.EventTime >= baselineFrom && e.EventTime < baselineUntil
                                     && e.Message != null
                                     && (e.Message.Contains("authentication failure") ||
                                         e.Message.Contains("failed password") ||
                                         e.Message.Contains("invalid user") ||
                                         e.Message.Contains("login failed")), ct);

                var observedCount = await db.LogEvents
                    .CountAsync(e => e.EventTime >= observeFrom
                                     && e.Message != null
                                     && (e.Message.Contains("authentication failure") ||
                                         e.Message.Contains("failed password") ||
                                         e.Message.Contains("invalid user") ||
                                         e.Message.Contains("login failed")), ct);

                baseline = baselineCount;
                observed = observedCount;
                break;
            }

            case "NewSourceIp":
            {
                // Count sources first seen (CreatedAt) in the observation window
                var newSourceCount = await db.Sources
                    .CountAsync(s => !s.IsDeleted && s.CreatedAt >= observeFrom, ct);
                var baselineNewSources = await db.Sources
                    .CountAsync(s => !s.IsDeleted &&
                                     s.CreatedAt >= baselineFrom && s.CreatedAt < baselineUntil, ct);

                baseline = baselineNewSources;
                observed = newSourceCount;
                break;
            }

            case "SilentSource":
            {
                // Sources that were online but haven't been seen since baseline window started
                var silentCount = await db.Sources
                    .CountAsync(s => !s.IsDeleted &&
                                     s.Status == "Online" &&
                                     s.LastSeen != null &&
                                     s.LastSeen < observeFrom, ct);

                // For silent source: fire if ANY silent source exceeds absolute minimum
                baseline = 0;
                observed = silentCount;
                break;
            }

            default:
                _logger.LogDebug("Unknown MetricType '{Type}' on rule id={Id} — skipped", rule.MetricType, rule.Id);
                return;
        }

        var threshold = Math.Max(baseline * rule.ThresholdMultiplier, rule.AbsoluteMinimum);
        if (observed <= threshold) return;

        var deviation = baseline > 0 ? observed / baseline : observed;

        _logger.LogInformation(
            "Anomaly detected: rule='{Name}' metric={Metric} observed={Obs:F2} baseline={Base:F2} threshold={Thr:F2}",
            rule.Name, rule.MetricType, observed, baseline, threshold);

        // Reload tracked entity to update fire stats
        var trackedRule = await db.AnomalyRules.FindAsync([rule.Id], ct);
        if (trackedRule is not null)
        {
            trackedRule.LastFiredAt = now;
            trackedRule.FireCount++;
        }

        var anomalyEvent = new AnomalyEvent
        {
            RuleId         = rule.Id,
            AffectedEntity = affectedEntity,
            BaselineValue  = Math.Round(baseline, 4),
            ObservedValue  = Math.Round(observed, 4),
            DeviationRatio = Math.Round(deviation, 4),
            Details        = $"Metric '{rule.MetricType}' observed {observed:F2} vs baseline {baseline:F2} " +
                             $"(threshold {threshold:F2}, multiplier {rule.ThresholdMultiplier}×)",
            Acknowledged   = false,
            DetectedAt     = now
        };
        db.AnomalyEvents.Add(anomalyEvent);

        // Auto-create incident if configured
        if (rule.AutoCreateIncident)
        {
            db.Incidents.Add(new Incident
            {
                Title       = $"[Anomaly] {rule.Name} — {rule.MetricType} spike detected",
                Description = anomalyEvent.Details,
                Status      = "Open",
                Priority    = rule.Severity == "Critical" ? "Critical"
                            : rule.Severity == "High"     ? "High"
                            : "Medium",
                CreatedAt   = now
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
