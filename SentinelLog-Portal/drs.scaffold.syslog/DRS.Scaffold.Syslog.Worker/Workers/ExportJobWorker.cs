using System.Text.Json;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Worker.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Picks up <c>exportjob</c> rows with status <c>Pending</c>, executes the
/// stored query, writes the output file (CSV or JSON), and updates status
/// to <c>Completed</c> or <c>Failed</c>.
/// </summary>
public sealed class ExportJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly WorkerOptions              _opts;
    private readonly ILogger<ExportJobWorker>   _logger;

    public ExportJobWorker(
        IServiceScopeFactory        scopeFactory,
        IOptions<WorkerOptions>     options,
        ILogger<ExportJobWorker>    logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExportJobWorker starting. Interval={Int}s  OutputDir={Dir}",
            _opts.ExportCheckIntervalSeconds, _opts.ExportOutputDir);

        var interval = TimeSpan.FromSeconds(_opts.ExportCheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportJobWorker error -- will retry next cycle");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("ExportJobWorker stopped.");
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        var pending = await db.ExportJobs
            .Where(j => !j.IsDeleted && j.Status == "Pending")
            .OrderBy(j => j.CreatedAt)
            .Take(5) // process up to 5 at a time
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        foreach (var job in pending)
        {
            job.Status    = "Running";
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            try
            {
                _logger.LogInformation("Processing export job id={Id} format={Format}", job.Id, job.Format);

                var filters = ParseQuery(job.QueryDefinition);

                // Build log query
                var query = db.LogEvents.Where(e => !e.IsDeleted).AsQueryable();

                if (filters.TryGetValue("severity", out var sev) && short.TryParse(sev, out var sevNum))
                    query = query.Where(e => e.Severity == sevNum);
                if (filters.TryGetValue("hostname", out var host) && !string.IsNullOrEmpty(host))
                    query = query.Where(e => e.Hostname != null && e.Hostname.Contains(host));
                if (filters.TryGetValue("program", out var prog) && !string.IsNullOrEmpty(prog))
                    query = query.Where(e => e.Program != null && e.Program.Contains(prog));
                if (filters.TryGetValue("search", out var search) && !string.IsNullOrEmpty(search))
                    query = query.Where(e => e.Message != null && e.Message.Contains(search));
                if (filters.TryGetValue("from", out var fromStr) && DateTimeOffset.TryParse(fromStr, out var from))
                    query = query.Where(e => e.EventTime >= from);
                if (filters.TryGetValue("to", out var toStr) && DateTimeOffset.TryParse(toStr, out var to))
                    query = query.Where(e => e.EventTime <= to);

                var results = await query
                    .OrderByDescending(e => e.EventTime)
                    .Take(50000)
                    .Select(e => new
                    {
                        e.Id, e.EventTime, e.Hostname, e.Severity, e.Facility,
                        e.Program, e.Message, e.DeviceType
                    })
                    .ToListAsync(ct);

                // Write file
                var dir = Path.Combine(AppContext.BaseDirectory, _opts.ExportOutputDir);
                Directory.CreateDirectory(dir);

                var ext      = job.Format?.ToLowerInvariant() == "json" ? "json" : "csv";
                var fileName = $"export_{job.Id}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.{ext}";
                var filePath = Path.Combine(dir, fileName);

                if (ext == "json")
                {
                    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(filePath, json, ct);
                }
                else
                {
                    var lines = new List<string> { "Id,EventTime,Hostname,Severity,Facility,Program,Message,DeviceType" };
                    foreach (var r in results)
                        lines.Add($"{r.Id},{r.EventTime:o},{Esc(r.Hostname)},{r.Severity},{r.Facility},{Esc(r.Program)},{Esc(r.Message)},{Esc(r.DeviceType)}");
                    await File.WriteAllLinesAsync(filePath, lines, ct);
                }

                job.Status    = "Completed";
                job.FilePath  = filePath;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                _logger.LogInformation("Export job id={Id} completed: {Count} rows -> {Path}",
                    job.Id, results.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export job id={Id} failed", job.Id);
                job.Status    = "Failed";
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static Dictionary<string, string> ParseQuery(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static string Esc(string? val) =>
        val is null ? "" : $"\"{val.Replace("\"", "\"\"")}\"";
}
