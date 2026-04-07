using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Worker.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Periodically evaluates source health based on <c>LastSeen</c> timestamps.
/// Marks sources as <c>Degraded</c> or <c>Offline</c> when no logs arrive
/// within configurable thresholds.
/// </summary>
public sealed class SourceHealthWorker : BackgroundService
{
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly WorkerOptions              _opts;
    private readonly ILogger<SourceHealthWorker> _logger;

    public SourceHealthWorker(
        IServiceScopeFactory        scopeFactory,
        IOptions<WorkerOptions>     options,
        ILogger<SourceHealthWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SourceHealthWorker starting. Interval={Int}s  Degraded={Deg}m  Offline={Off}m",
            _opts.HealthCheckIntervalSeconds, _opts.DegradedAfterMinutes, _opts.OfflineAfterMinutes);

        var interval = TimeSpan.FromSeconds(_opts.HealthCheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SourceHealthWorker error -- will retry next cycle");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("SourceHealthWorker stopped.");
    }

    private async Task EvaluateHealthAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();
        var now = DateTimeOffset.UtcNow;

        var degradedCutoff = now.AddMinutes(-_opts.DegradedAfterMinutes);
        var offlineCutoff  = now.AddMinutes(-_opts.OfflineAfterMinutes);

        var sources = await db.Sources
            .Where(s => !s.IsDeleted && s.Status != "Pending")
            .ToListAsync(ct);

        int degraded = 0, offline = 0;

        foreach (var s in sources)
        {
            if (s.LastSeen is null) continue;

            var prev = s.Status;

            if (s.LastSeen < offlineCutoff && s.Status != "Offline")
            {
                s.Status    = "Offline";
                s.UpdatedAt = now;
                s.UpdatedBy = _opts.ServiceAccount;
                offline++;
            }
            else if (s.LastSeen < degradedCutoff && s.LastSeen >= offlineCutoff && s.Status != "Degraded")
            {
                s.Status    = "Degraded";
                s.UpdatedAt = now;
                s.UpdatedBy = _opts.ServiceAccount;
                degraded++;
            }
        }

        if (degraded > 0 || offline > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Source health updated: {Degraded} degraded, {Offline} offline", degraded, offline);
        }
    }
}
