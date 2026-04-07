using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Worker.Extensions;
using DRS.Scaffold.Syslog.Worker.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Picks up enabled <see cref="LogForwarder"/> destinations and relays recent
/// <see cref="LogEvent"/> rows to external SIEMs or log aggregators.
/// Supports UDP/TCP (RFC5424, RFC3164, JSON, CEF) and HTTP/HTTPS (JSON payload).
/// </summary>
public sealed class LogForwarderWorker : BackgroundService
{
    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly IHttpClientFactory            _httpFactory;
    private readonly WorkerOptions                 _opts;
    private readonly ILogger<LogForwarderWorker>   _logger;

    public LogForwarderWorker(
        IServiceScopeFactory           scopeFactory,
        IHttpClientFactory             httpFactory,
        IOptions<WorkerOptions>        options,
        ILogger<LogForwarderWorker>    logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory  = httpFactory;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "LogForwarderWorker starting. Interval={Interval}s  BatchSize={Batch}",
            _opts.ForwarderIntervalSeconds, _opts.ForwarderBatchSize);

        var interval = TimeSpan.FromSeconds(_opts.ForwarderIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ForwardCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LogForwarderWorker cycle error");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("LogForwarderWorker stopped.");
    }

    private async Task ForwardCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        var forwarders = await db.LogForwarders
            .Where(f => f.Enabled && !f.IsDeleted)
            .ToListAsync(ct);

        if (forwarders.Count == 0) return;

        foreach (var fwd in forwarders)
        {
            try
            {
                await ForwardToDestinationAsync(db, fwd, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Forwarder id={Id} '{Name}' failed", fwd.Id, fwd.Name);
                fwd.ErrorCount++;
                fwd.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task ForwardToDestinationAsync(
        SyslogDbContext db, LogForwarder fwd, CancellationToken ct)
    {
        var since = fwd.LastForwardedAt ?? DateTimeOffset.UtcNow.AddSeconds(-_opts.ForwarderIntervalSeconds * 2);

        var query = db.LogEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.EventTime > since)
            .OrderBy(e => e.EventTime);

        // Apply optional severity filter (e.g. "0,1,2" → only Critical/Alert/Emergency)
        if (!string.IsNullOrWhiteSpace(fwd.SeverityFilter))
        {
            var sevs = fwd.SeverityFilter
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => short.TryParse(s.Trim(), out var v) ? (short?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToHashSet();
            if (sevs.Count > 0)
                query = (IOrderedQueryable<LogEvent>)query.Where(e => e.Severity != null && sevs.Contains(e.Severity.Value));
        }

        var events = await query.Take(_opts.ForwarderBatchSize).ToListAsync(ct);
        if (events.Count == 0) return;

        var protocol = fwd.Protocol?.ToUpperInvariant();

        long forwarded = 0;
        long errors    = 0;

        if (protocol == "HTTP" || protocol == "HTTPS")
        {
            (forwarded, errors) = await ForwardHttpAsync(fwd, events, ct);
        }
        else // UDP / TCP / TLS (all treated as syslog line protocol)
        {
            (forwarded, errors) = await ForwardSyslogAsync(fwd, events, ct);
        }

        // Update stats
        var trackedFwd = await db.LogForwarders.FindAsync([fwd.Id], ct);
        if (trackedFwd is not null)
        {
            trackedFwd.ForwardedCount  += forwarded;
            trackedFwd.ErrorCount      += errors;
            trackedFwd.LastForwardedAt  = events[^1].EventTime;
            trackedFwd.UpdatedAt        = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Forwarder '{Name}' ({Protocol}://{Host}:{Port}) — forwarded={F}  errors={E}",
            fwd.Name, fwd.Protocol, fwd.Host, fwd.Port, forwarded, errors);
    }

    // ── HTTP/HTTPS forwarding ─────────────────────────────────────────────────

    private async Task<(long forwarded, long errors)> ForwardHttpAsync(
        LogForwarder fwd, List<LogEvent> events, CancellationToken ct)
    {
        try
        {
            var client  = _httpFactory.CreateClient();
            var url     = $"{fwd.Protocol?.ToLower()}://{fwd.Host}:{fwd.Port}";
            var payload = events.Select(e => new
            {
                eventTime  = e.EventTime,
                hostname   = e.Hostname,
                severity   = e.Severity,
                facility   = e.Facility,
                program    = e.Program,
                message    = e.Message,
                deviceType = e.DeviceType
            });

            using var resp = await client.PostAsJsonAsync(url, payload, ct);
            if (resp.IsSuccessStatusCode)
                return (events.Count, 0);

            _logger.LogWarning("HTTP forwarder '{Name}' returned {Status}", fwd.Name, (int)resp.StatusCode);
            return (0, events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP forwarder '{Name}' exception", fwd.Name);
            return (0, events.Count);
        }
    }

    // ── UDP/TCP syslog forwarding ─────────────────────────────────────────────

    private async Task<(long forwarded, long errors)> ForwardSyslogAsync(
        LogForwarder fwd, List<LogEvent> events, CancellationToken ct)
    {
        long forwarded = 0;
        long errors    = 0;

        if (string.IsNullOrWhiteSpace(fwd.Host) || fwd.Port <= 0)
        {
            _logger.LogWarning("Forwarder '{Name}' has no valid host/port configured", fwd.Name);
            return (0, events.Count);
        }

        foreach (var e in events)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var line = FormatSyslogLine(fwd.Format, e);
                var data = Encoding.UTF8.GetBytes(line + "\n");

                if (fwd.Protocol?.ToUpperInvariant() == "UDP")
                {
                    using var udp = new UdpClient();
                    await udp.SendAsync(data.AsMemory(), fwd.Host, fwd.Port, ct);
                }
                else // TCP / TLS (TLS without cert validation for now)
                {
                    using var tcp    = new TcpClient();
                    await tcp.ConnectAsync(fwd.Host, fwd.Port, ct);
                    var stream = tcp.GetStream();
                    await stream.WriteAsync(data, ct);
                }

                forwarded++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Syslog forwarder '{Name}' failed on event id={Id}", fwd.Name, e.Id);
                errors++;
                if (errors > 10) break; // stop hammering a dead endpoint
            }
        }

        return (forwarded, errors);
    }

    // ── Format helpers ────────────────────────────────────────────────────────

    private static string FormatSyslogLine(string? format, LogEvent e)
    {
        var ts  = e.EventTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var sev = e.Severity ?? 6;
        var fac = e.Facility ?? 1;
        var pri = fac * 8 + sev;

        return (format?.ToUpperInvariant()) switch
        {
            "RFC3164" =>
                $"<{pri}>{ts} {e.Hostname ?? "-"} {e.Program ?? "-"}: {e.Message}",

            "JSON" =>
                $"{{\"time\":\"{ts}\",\"host\":\"{Esc(e.Hostname)}\",\"sev\":{sev},\"prog\":\"{Esc(e.Program)}\",\"msg\":\"{Esc(e.Message)}\"}}",

            "CEF" =>
                $"CEF:0|SentinelLog|SyslogAgent|1.0|{e.Program ?? ""}|{e.Message?.Truncate(100) ?? ""}|{MapSevCef(sev)}|shost={e.Hostname ?? ""}",

            "LEEF" =>
                $"LEEF:2.0|SentinelLog|SyslogAgent|1.0|{e.Program ?? ""}|\tshost={e.Hostname ?? ""}\tsev={sev}\tmsg={e.Message?.Truncate(200) ?? ""}",

            _ => // RFC5424 default
                $"<{pri}>1 {ts} {e.Hostname ?? "-"} {e.Program ?? "-"} - - - {e.Message}",
        };
    }

    private static string Esc(string? s) => s?.Replace("\"", "\\\"").Replace("\n", " ") ?? "";
    private static int MapSevCef(int sev) => sev switch { 0 => 10, 1 => 10, 2 => 9, 3 => 7, 4 => 5, 5 => 3, 6 => 1, _ => 0 };
}
