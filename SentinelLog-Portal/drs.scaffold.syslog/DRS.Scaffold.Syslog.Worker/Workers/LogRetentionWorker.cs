using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Worker.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Enforces log retention policies. Deletes <c>logevent</c> rows older than
/// the per-source-type retention period defined in <c>storagepolicy</c>.
/// Falls back to <see cref="WorkerOptions.DefaultRetentionDays"/> when no
/// explicit policy exists for a source type.
/// Also purges processed <c>systemlogs</c> staging rows older than 7 days.
/// </summary>
public sealed class LogRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory        _scopeFactory;
    private readonly WorkerOptions               _opts;
    private readonly ILogger<LogRetentionWorker> _logger;

    public LogRetentionWorker(
        IServiceScopeFactory         scopeFactory,
        IOptions<WorkerOptions>      options,
        ILogger<LogRetentionWorker>  logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "LogRetentionWorker starting. Interval={Int}s  DefaultRetention={Ret}d",
            _opts.RetentionIntervalSeconds, _opts.DefaultRetentionDays);

        var interval = TimeSpan.FromSeconds(_opts.RetentionIntervalSeconds);

        // Delay first run by 30s so the poller can get ahead
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LogRetentionWorker error -- will retry next cycle");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("LogRetentionWorker stopped.");
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        // Load retention policies
        var policies = await db.StoragePolicies
            .Where(p => !p.IsDeleted && p.RetentionDays != null)
            .ToListAsync(ct);

        int totalDeleted = 0;

        // Per-source-type purge
        foreach (var policy in policies)
        {
            if (policy.RetentionDays is null or <= 0) continue;
            var cutoff = DateTimeOffset.UtcNow.AddDays(-policy.RetentionDays.Value);

            var deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM logevent WHERE devicetype = {policy.SourceType} AND eventtime < {cutoff} AND isdeleted = false",
                ct);
            totalDeleted += deleted;

            if (deleted > 0)
                _logger.LogInformation(
                    "Retention: purged {Count} logevent rows for type={Type} older than {Days}d",
                    deleted, policy.SourceType, policy.RetentionDays);
        }

        // Default retention for types without explicit policy
        var explicitTypes = policies
            .Where(p => p.SourceType is not null)
            .Select(p => p.SourceType!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_opts.DefaultRetentionDays > 0)
        {
            var defaultCutoff = DateTimeOffset.UtcNow.AddDays(-_opts.DefaultRetentionDays);

            // Build a raw SQL with NOT IN for explicit types
            if (explicitTypes.Count > 0)
            {
                // Use parameterized query for each explicit type
                var deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM logevent WHERE eventtime < {defaultCutoff} AND isdeleted = false AND (devicetype IS NULL OR devicetype NOT IN (SELECT sourcetype FROM storagepolicy WHERE isdeleted = false AND sourcetype IS NOT NULL))",
                    ct);
                totalDeleted += deleted;

                if (deleted > 0)
                    _logger.LogInformation(
                        "Retention: purged {Count} logevent rows (default {Days}d policy)",
                        deleted, _opts.DefaultRetentionDays);
            }
            else
            {
                var deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM logevent WHERE eventtime < {defaultCutoff} AND isdeleted = false",
                    ct);
                totalDeleted += deleted;

                if (deleted > 0)
                    _logger.LogInformation(
                        "Retention: purged {Count} logevent rows (default {Days}d policy)",
                        deleted, _opts.DefaultRetentionDays);
            }
        }

        // Purge old staging rows (systemlogs older than 7 days)
        var stagingCutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var stagingDeleted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM systemlogs WHERE log_datetime < {stagingCutoff}",
            ct);

        if (stagingDeleted > 0)
            _logger.LogInformation("Retention: purged {Count} systemlogs staging rows older than 7d", stagingDeleted);

        if (totalDeleted > 0 || stagingDeleted > 0)
            _logger.LogInformation("Retention cycle complete: {Total} logevent + {Staging} systemlogs purged",
                totalDeleted, stagingDeleted);
    }
}
