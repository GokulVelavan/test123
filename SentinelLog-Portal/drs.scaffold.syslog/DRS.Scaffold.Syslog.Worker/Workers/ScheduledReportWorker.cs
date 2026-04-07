using System.Text.Json;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Worker.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Checks <c>scheduledreport</c> rows on a timer and executes any that are
/// due based on their cron schedule. Updates <c>LastRunAt</c> after execution.
/// Uses a simple cron subset parser (minute hour day-of-month month day-of-week).
/// </summary>
public sealed class ScheduledReportWorker : BackgroundService
{
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly WorkerOptions                   _opts;
    private readonly ILogger<ScheduledReportWorker>  _logger;

    public ScheduledReportWorker(
        IServiceScopeFactory            scopeFactory,
        IOptions<WorkerOptions>         options,
        ILogger<ScheduledReportWorker>  logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledReportWorker starting. Interval={Int}s",
            _opts.ReportCheckIntervalSeconds);

        var interval = TimeSpan.FromSeconds(_opts.ReportCheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduledReportWorker error -- will retry next cycle");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("ScheduledReportWorker stopped.");
    }

    private async Task CheckAndRunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();
        var now = DateTimeOffset.UtcNow;

        var reports = await db.ScheduledReports
            .Where(r => !r.IsDeleted && r.Enabled)
            .ToListAsync(ct);

        foreach (var report in reports)
        {
            if (!IsDue(report.Schedule, report.LastRunAt, now)) continue;

            try
            {
                _logger.LogInformation("Executing scheduled report '{Name}' (id={Id})", report.Name, report.Id);

                // Parse filters from JSON
                var filters = ParseFilters(report.FiltersJson);

                // Build query
                var query = db.LogEvents.Where(e => !e.IsDeleted).AsQueryable();

                if (filters.TryGetValue("severity", out var sev) && !string.IsNullOrEmpty(sev))
                {
                    if (short.TryParse(sev, out var sevNum))
                        query = query.Where(e => e.Severity == sevNum);
                }
                if (filters.TryGetValue("hostname", out var host) && !string.IsNullOrEmpty(host))
                    query = query.Where(e => e.Hostname != null && e.Hostname.Contains(host));
                if (filters.TryGetValue("days", out var daysStr) && int.TryParse(daysStr, out var days))
                    query = query.Where(e => e.EventTime >= now.AddDays(-days));
                else
                    query = query.Where(e => e.EventTime >= now.AddDays(-7)); // default 7 days

                var results = await query
                    .OrderByDescending(e => e.EventTime)
                    .Take(10000)
                    .Select(e => new { e.Id, e.EventTime, e.Hostname, e.Severity, e.Program, e.Message })
                    .ToListAsync(ct);

                // Write to exports directory
                var dir = Path.Combine(AppContext.BaseDirectory, _opts.ExportOutputDir, "reports");
                Directory.CreateDirectory(dir);

                var fileName = $"report_{report.Id}_{now:yyyyMMdd_HHmmss}.{report.Format}";
                var filePath = Path.Combine(dir, fileName);

                if (report.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(filePath, json, ct);
                }
                else // CSV
                {
                    var lines = new List<string> { "Id,EventTime,Hostname,Severity,Program,Message" };
                    foreach (var r in results)
                        lines.Add($"{r.Id},{r.EventTime:o},{Escape(r.Hostname)},{r.Severity},{Escape(r.Program)},{Escape(r.Message)}");
                    await File.WriteAllLinesAsync(filePath, lines, ct);
                }

                report.LastRunAt = now;
                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Report '{Name}' generated: {Count} rows -> {Path}",
                    report.Name, results.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute report '{Name}' (id={Id})", report.Name, report.Id);
            }
        }
    }

    /// <summary>
    /// Simple cron check: returns true if enough time has elapsed since last run
    /// based on the cron expression. Supports common presets and basic 5-field cron.
    /// </summary>
    private static bool IsDue(string? schedule, DateTimeOffset? lastRun, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(schedule)) return false;

        // Calculate minimum interval from schedule
        var minInterval = schedule.Trim().ToLowerInvariant() switch
        {
            "*/5 * * * *"  => TimeSpan.FromMinutes(5),
            "*/15 * * * *" => TimeSpan.FromMinutes(15),
            "*/30 * * * *" => TimeSpan.FromMinutes(30),
            "0 * * * *"    => TimeSpan.FromHours(1),
            "0 */6 * * *"  => TimeSpan.FromHours(6),
            "0 0 * * *"    => TimeSpan.FromHours(24),
            "0 0 * * 1"    => TimeSpan.FromDays(7),
            "0 0 1 * *"    => TimeSpan.FromDays(30),
            _ => ParseCronInterval(schedule)
        };

        if (lastRun is null) return true; // never run before
        return (now - lastRun.Value) >= minInterval;
    }

    private static TimeSpan ParseCronInterval(string cron)
    {
        // Fallback: parse minute field for basic intervals
        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1 && parts[0].StartsWith("*/") &&
            int.TryParse(parts[0][2..], out var mins))
            return TimeSpan.FromMinutes(mins);

        // Default to daily if we can't parse
        return TimeSpan.FromHours(24);
    }

    private static Dictionary<string, string> ParseFilters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static string Escape(string? val) =>
        val is null ? "" : $"\"{val.Replace("\"", "\"\"")}\"";
}
