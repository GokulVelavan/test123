using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Worker.Evaluation;
using DRS.Scaffold.Syslog.Worker.Extensions;
using DRS.Scaffold.Syslog.Worker.Metrics;
using DRS.Scaffold.Syslog.Worker.Options;
using DRS.Scaffold.Syslog.Worker.Parsing;
using DRS.Scaffold.Syslog.Worker.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Background worker that:
/// <list type="number">
///   <item>Polls <c>systemlogs</c> for new rows beyond the watermark.</item>
///   <item>Parses and cleans each row (strips Python b'...' wrappers) and inserts into <c>logevent</c>.</item>
///   <item>Evaluates all active <c>alertrule</c> entries and inserts matching <c>alertevent</c> rows.</item>
///   <item>Accumulates throughput metrics and flushes them to <c>systemmetric</c> on a separate timer.</item>
/// </list>
/// </summary>
public sealed class SyslogPollerWorker : BackgroundService
{
    private readonly IServiceScopeFactory        _scopeFactory;
    private readonly WorkerOptions               _opts;
    private readonly PollerWatermark             _watermark;
    private readonly MetricAccumulator           _metrics;
    private readonly ILogger<SyslogPollerWorker> _logger;

    public SyslogPollerWorker(
        IServiceScopeFactory            scopeFactory,
        IOptions<WorkerOptions>         options,
        PollerWatermark                 watermark,
        MetricAccumulator               metrics,
        ILogger<SyslogPollerWorker>     logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = options.Value;
        _watermark    = watermark;
        _metrics      = metrics;
        _logger       = logger;
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Entry point
    // ?????????????????????????????????????????????????????????????????????????

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SyslogPollerWorker starting. PollInterval={Interval}s  Batch={Batch}  MetricFlush={Flush}s",
            _opts.PollIntervalSeconds, _opts.BatchSize, _opts.MetricFlushIntervalSeconds);

        await SeedDefaultAlertRulesAsync(stoppingToken);
        await CleanExistingLogeventsAsync(stoppingToken);
        await InitialiseWatermarkAsync(stoppingToken);

        var pollInterval    = TimeSpan.FromSeconds(_opts.PollIntervalSeconds);
        var metricInterval  = TimeSpan.FromSeconds(_opts.MetricFlushIntervalSeconds);
        var nextMetricFlush = DateTime.UtcNow.Add(metricInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollCycleAsync(stoppingToken);

                if (DateTime.UtcNow >= nextMetricFlush)
                {
                    await FlushMetricsAsync(stoppingToken);
                    nextMetricFlush = DateTime.UtcNow.Add(metricInterval);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error in poll cycle � will retry in {Interval}s",
                    _opts.PollIntervalSeconds);
            }

            await Task.Delay(pollInterval, stoppingToken);
        }

        // Final metric flush on graceful shutdown
        await FlushMetricsAsync(CancellationToken.None);
        _logger.LogInformation("SyslogPollerWorker stopped.");
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Startup: seed default alert rules when the table is empty
    // ?????????????????????????????????????????????????????????????????????????

    private async Task SeedDefaultAlertRulesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        if (await db.AlertRules.AnyAsync(ct))
        {
            _logger.LogInformation("Alert rules already exist � skipping seed.");
            return;
        }

        var now   = DateTimeOffset.UtcNow;
        var rules = new List<AlertRule>
        {
            new() {
                Name                = "Security Alert",
                Description         = "Fires on any message containing 'security' or 'Security'",
                Severity            = "Warning",
                ConditionExpression = "Security",
                SourceType          = null,
                TimeWindow          = 60,
                Enabled             = true,
                CreatedAt           = now,
                CreatedBy           = _opts.ServiceAccount
            },
            new() {
                Name                = "SMT Unreachable",
                Description         = "Fires when syslog server cannot be reached",
                Severity            = "Critical",
                ConditionExpression = "cannot be reached",
                SourceType          = null,
                TimeWindow          = 60,
                Enabled             = true,
                CreatedAt           = now,
                CreatedBy           = _opts.ServiceAccount
            },
            new() {
                Name                = "Error Detected",
                Description         = "Fires on any message containing 'error' or 'err'",
                Severity            = "Error",
                ConditionExpression = "error",
                SourceType          = null,
                TimeWindow          = 60,
                Enabled             = true,
                CreatedAt           = now,
                CreatedBy           = _opts.ServiceAccount
            },
            new() {
                Name                = "Authentication Failure",
                Description         = "Fires on login or authentication failure messages",
                Severity            = "Critical",
                ConditionExpression = "authentication failure OR login failed OR invalid user",
                SourceType          = null,
                TimeWindow          = 300,
                Enabled             = true,
                CreatedAt           = now,
                CreatedBy           = _opts.ServiceAccount
            },
            new() {
                Name                = "Connection Refused",
                Description         = "Fires when a connection is refused",
                Severity            = "Warning",
                ConditionExpression = "connection refused OR connection reset",
                SourceType          = null,
                TimeWindow          = 60,
                Enabled             = true,
                CreatedAt           = now,
                CreatedBy           = _opts.ServiceAccount
            }
        };

        db.AlertRules.AddRange(rules);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} default alert rules.", rules.Count);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Startup: clean previously stored logevent rows that have b'...' wrappers
    // ?????????????????????????????????????????????????????????????????????????

    private async Task CleanExistingLogeventsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        // Find rows whose hostname still contains the Python b'...' wrapper
        var dirtyRows = await db.LogEvents
            .Where(l => l.Hostname != null && l.Hostname.StartsWith("b'"))
            .ToListAsync(ct);

        if (dirtyRows.Count == 0)
        {
            _logger.LogInformation("No dirty logevent rows found � no cleanup needed.");
            return;
        }

        _logger.LogInformation("Cleaning {Count} logevent row(s) with b'...' wrappers�", dirtyRows.Count);

        foreach (var row in dirtyRows)
        {
            row.Hostname   = SyslogParser.Unwrap(row.Hostname);
            row.Program    = SyslogParser.Unwrap(row.Program);
            row.Message    = SyslogParser.Unwrap(row.Message);
            row.RawMessage = SyslogParser.Unwrap(row.RawMessage);
            row.UpdatedAt  = DateTimeOffset.UtcNow;
            row.UpdatedBy  = _opts.ServiceAccount;

            // Re-derive severity from the now-clean message
            if (row.Severity is null && row.Message is not null)
                row.Severity = SyslogParser.DetectSeverityPublic(row.Message);

            // Re-derive device type
            row.DeviceType = SyslogParser.InferDeviceTypePublic(row.Program);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Cleanup complete � {Count} logevent row(s) updated.", dirtyRows.Count);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Watermark initialisation
    // ?????????????????????????????????????????????????????????????????????????

    private async Task InitialiseWatermarkAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        // 1. Try to load a previously persisted watermark from platformconfig
        var stored = await db.PlatformConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Category == PollerWatermark.Category && c.Key == PollerWatermark.Key, ct);

        if (stored is not null && int.TryParse(stored.Value, out var persistedId) && persistedId > 0)
        {
            _watermark.Seed(persistedId);
            _logger.LogInformation(
                "Watermark loaded from DB: {Id} — resuming from last processed row", persistedId);
            return;
        }

        // 2. No persisted watermark — fall back to config option
        if (_opts.StartFromCurrentWatermark)
        {
            var maxId = await db.SystemLogs.MaxAsync(s => (int?)s.Id, ct) ?? 0;
            _watermark.Seed(maxId);
            _logger.LogInformation(
                "No persisted watermark — StartFromCurrentWatermark=true, set to {Id} (skipping existing rows)", maxId);
        }
        else
        {
            _watermark.Reset();
            _logger.LogInformation(
                "No persisted watermark — StartFromCurrentWatermark=false, processing all rows from beginning");
        }
    }

    private async Task PersistWatermarkAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();
        var now = DateTimeOffset.UtcNow;

        var record = await db.PlatformConfigs
            .FirstOrDefaultAsync(
                c => c.Category == PollerWatermark.Category && c.Key == PollerWatermark.Key, ct);

        if (record is null)
        {
            db.PlatformConfigs.Add(new Core.Models.PlatformConfig
            {
                Category  = PollerWatermark.Category,
                Key       = PollerWatermark.Key,
                Value     = _watermark.LastProcessedId.ToString(),
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            record.Value     = _watermark.LastProcessedId.ToString();
            record.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Main poll cycle
    // ?????????????????????????????????????????????????????????????????????????

    private async Task PollCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        // 1. Fetch new raw rows beyond the current watermark
        var rawRows = await db.SystemLogs
            .AsNoTracking()
            .Where(s => s.Id > _watermark.LastProcessedId)
            .OrderBy(s => s.Id)
            .Take(_opts.BatchSize)
            .ToListAsync(ct);

        if (rawRows.Count == 0)
        {
            _logger.LogDebug("No new systemlogs rows (watermark={W})", _watermark.LastProcessedId);
            return;
        }

        _logger.LogInformation(
            "Fetched {Count} new systemlogs row(s) (watermark={W})",
            rawRows.Count, _watermark.LastProcessedId);

        // 2. Load active alert rules and registered sources once per cycle
        var activeRules = await db.AlertRules
            .AsNoTracking()
            .Where(r => r.Enabled && !r.IsDeleted)
            .ToListAsync(ct);

        // Tracked query so LastSeen / Status changes are persisted
        var sources = await db.Sources
            .Where(s => !s.IsDeleted)
            .ToListAsync(ct);

        // 3. Parse every raw row -> LogEvent, auto-discover unknown sources
        var newLogEvents   = new List<LogEvent>(rawRows.Count);
        var newAlertEvents = new List<AlertEvent>();
        var now            = DateTimeOffset.UtcNow;

        foreach (var raw in rawRows)
        {
            try
            {
                var logEvent = SyslogParser.Parse(raw, _opts.ServiceAccount);

                var cleanIp   = SyslogParser.Unwrap(raw.SourceIp);
                var cleanHost = SyslogParser.Unwrap(raw.Host);

                var source = sources.FirstOrDefault(s =>
                    (!string.IsNullOrWhiteSpace(cleanIp) &&
                      string.Equals(s.IpAddress, cleanIp, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(cleanHost) &&
                      string.Equals(s.Name, cleanHost, StringComparison.OrdinalIgnoreCase)));

                // Auto-discover: create a new source if none matched
                if (source is null && !string.IsNullOrWhiteSpace(cleanHost))
                {
                    source = new LogSource
                    {
                        Name       = cleanHost,
                        IpAddress  = cleanIp,
                        DeviceType = logEvent.DeviceType ?? "Generic",
                        Status     = "Online",
                        LastSeen   = now,
                        CreatedAt  = now,
                        CreatedBy  = _opts.ServiceAccount
                    };
                    db.Sources.Add(source);
                    await db.SaveChangesAsync(ct);          // flush to get DB-assigned Id
                    sources.Add(source);                    // add to in-memory cache
                    _logger.LogInformation(
                        "Auto-discovered source '{Host}' (ip={Ip}, type={Type}) -> id={Id}",
                        cleanHost, cleanIp, source.DeviceType, source.Id);
                }

                if (source is not null)
                {
                    logEvent.SourceId = source.Id;
                    source.LastSeen   = now;
                    source.Status     = "Online";
                    source.UpdatedAt  = now;
                    source.UpdatedBy  = _opts.ServiceAccount;
                }

                newLogEvents.Add(logEvent);
            }
            catch (Exception ex)
            {
                // A bad row must never abort the whole batch
                _logger.LogWarning(ex,
                    "Skipping systemlogs id={Id} -- parse error: {Msg}", raw.Id, ex.Message);
            }
        }

        if (newLogEvents.Count == 0)
        {
            _watermark.Advance(rawRows[^1].Id);
            await PersistWatermarkAsync(ct);
            return;
        }

        // 4. Bulk-insert LogEvents so EF assigns DB IDs
        db.LogEvents.AddRange(newLogEvents);
        try
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Inserted {Count} logevent row(s)", newLogEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SaveChangesAsync failed inserting logevent rows; watermark NOT advanced so batch will retry");
            return;
        }

        // Record metrics for each inserted event
        foreach (var le in newLogEvents)
            _metrics.RecordEvent(le.Severity);

        // 5. Evaluate alert rules against each new LogEvent
        foreach (var logEvent in newLogEvents)
        {
            foreach (var rule in AlertRuleEvaluator.Evaluate(logEvent, activeRules))
            {
                newAlertEvents.Add(new AlertEvent
                {
                    RuleId      = rule.Id,
                    LogEventId  = logEvent.Id,
                    Severity    = rule.Severity,
                    Message     = BuildAlertMessage(rule, logEvent),
                    TriggeredAt = DateTimeOffset.UtcNow,
                    CreatedAt   = DateTimeOffset.UtcNow,
                    CreatedBy   = _opts.ServiceAccount
                });
                _metrics.RecordAlert();
            }
        }

        // 5b. Threat Intel auto-match — flag events whose sourceIp/hostname is a known IOC
        try
        {
            var activeIocs = await db.ThreatIndicators
                .AsNoTracking()
                .Where(t => t.IsActive && !t.IsDeleted &&
                            (t.ExpiresAt == null || t.ExpiresAt > DateTimeOffset.UtcNow) &&
                            (t.Type == "IP" || t.Type == "Domain"))
                .Select(t => new { t.Id, t.Type, t.Value, t.ThreatCategory, t.Confidence })
                .ToListAsync(ct);

            if (activeIocs.Count > 0)
            {
                var iocIps     = activeIocs.Where(i => i.Type == "IP")
                                           .ToDictionary(i => i.Value.Trim().ToLowerInvariant(), i => i);
                var iocDomains = activeIocs.Where(i => i.Type == "Domain")
                                           .ToDictionary(i => i.Value.Trim().ToLowerInvariant(), i => i);

                foreach (var le in newLogEvents)
                {
                    var host = le.Hostname?.Trim().ToLowerInvariant();

                    if (host != null && iocIps.TryGetValue(host, out var iocIpByHost))
                    {
                        newAlertEvents.Add(new AlertEvent
                        {
                            RuleId      = null,
                            LogEventId  = le.Id,
                            Severity    = "Critical",
                            Message     = $"[ThreatIntel] Host {le.Hostname} matches known malicious IP IOC " +
                                          $"(category={iocIpByHost.ThreatCategory}, confidence={iocIpByHost.Confidence}%)",
                            TriggeredAt = DateTimeOffset.UtcNow,
                            CreatedAt   = DateTimeOffset.UtcNow,
                            CreatedBy   = _opts.ServiceAccount
                        });
                    }
                    else if (host != null && iocDomains.TryGetValue(host, out var iocDomain))
                    {
                        newAlertEvents.Add(new AlertEvent
                        {
                            RuleId      = null,
                            LogEventId  = le.Id,
                            Severity    = "Critical",
                            Message     = $"[ThreatIntel] Hostname {le.Hostname} matches IOC domain " +
                                          $"(category={iocDomain.ThreatCategory}, confidence={iocDomain.Confidence}%)",
                            TriggeredAt = DateTimeOffset.UtcNow,
                            CreatedAt   = DateTimeOffset.UtcNow,
                            CreatedBy   = _opts.ServiceAccount
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Threat Intel IOC check failed — skipping for this cycle");
        }

        // 6. Persist alert events
        if (newAlertEvents.Count > 0)
        {
            db.AlertEvents.AddRange(newAlertEvents);
            try
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Inserted {Count} alertevent row(s)", newAlertEvents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveChangesAsync failed inserting alertevent rows");
            }
        }

        // 7. Advance watermark and persist to DB so restarts don't reprocess
        _watermark.Advance(rawRows[^1].Id);
        await PersistWatermarkAsync(ct);

        _logger.LogInformation(
            "Cycle complete — logevents={E}  alertevents={A}  watermark={W}",
            newLogEvents.Count, newAlertEvents.Count, _watermark.LastProcessedId);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Metric flush
    // ?????????????????????????????????????????????????????????????????????????

    private async Task FlushMetricsAsync(CancellationToken ct)
    {
        var snapshot = _metrics.Drain();
        if (snapshot.EventsProcessed == 0 && snapshot.AlertsFired == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();
        var now = DateTimeOffset.UtcNow;

        var rows = new List<SystemMetric>
        {
            Metric("worker.events_processed", snapshot.EventsProcessed, now),
            Metric("worker.alerts_fired",      snapshot.AlertsFired,     now),
            Metric("worker.eps",
                snapshot.EventsProcessed / (double)Math.Max(_opts.MetricFlushIntervalSeconds, 1), now)
        };

        // Per-severity breakdown rows
        string[] labels = ["emergency","alert","critical","error","warning","notice","info","debug"];
        for (var i = 0; i < 8; i++)
            if (snapshot.SeverityCounts[i] > 0)
                rows.Add(Metric($"worker.severity.{labels[i]}", snapshot.SeverityCounts[i], now));

        db.SystemMetrics.AddRange(rows);
        try
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Metrics flushed � events={E}  alerts={A}  EPS={Eps:F2}",
                snapshot.EventsProcessed, snapshot.AlertsFired,
                snapshot.EventsProcessed / (double)Math.Max(_opts.MetricFlushIntervalSeconds, 1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics to systemmetric table");
        }
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Helpers
    // ?????????????????????????????????????????????????????????????????????????

    private SystemMetric Metric(string name, double value, DateTimeOffset at) => new()
    {
        Name       = name,
        Value      = value,
        MetricTime = at,
        CreatedAt  = at,
        CreatedBy  = _opts.ServiceAccount
    };

    private static string BuildAlertMessage(AlertRule rule, LogEvent evt) =>
        $"Rule '{rule.Name}' triggered on host '{evt.Hostname}' " +
        $"[severity={SyslogParser.SeverityLabel(evt.Severity)}]: {evt.Message?.Truncate(200)}";
}
