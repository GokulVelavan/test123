using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Linux;

/// <summary>
/// Batch collector for binary login accounting files:
///   - /var/log/wtmp  — successful logins/logouts/reboots    (read via `last -F`)
///   - /var/log/btmp  — failed login attempts                (read via `lastb -F`)
///   - /var/log/lastlog — last successful login per user     (read via `lastlog`)
///
/// These files are binary and cannot be tailed as text. Instead, the collector
/// runs the corresponding OS commands and tracks the latest timestamp seen in each
/// file so every batch run emits only new entries.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxBinaryLogCollector : IBatchCollector
{
    // Deduplication store keys
    private const string WtmpKey = "binlog:wtmp:last_ts";
    private const string BtmpKey = "binlog:btmp:last_ts";

    // `last -F` output (GNU/Linux):
    // user   pts/0   192.168.1.5  Mon Jan 15 10:23:45 2024 - Mon Jan 15 11:00:00 2024  (00:36)
    // reboot system boot  Mon Jan 15 10:00:00 2024   still running
    private static readonly Regex LastLineRegex = new(
        @"^(\S+)\s+(\S+)\s+(\S*)\s+((?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)\s+\w+\s+\d+\s+\d{2}:\d{2}:\d{2}\s+\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] LastDateFormats =
    {
        "ddd MMM  d HH:mm:ss yyyy",  // single-digit day with double-space padding (GNU last)
        "ddd MMM d HH:mm:ss yyyy",
        "ddd MMM dd HH:mm:ss yyyy"
    };

    // lastlog format: Username  Port  From  Latest (with timezone offset)
    // e.g. root   pts/0  192.168.1.1  Mon Jan 15 10:00:00 +0000 2024
    private static readonly Regex LastlogLineRegex = new(
        @"^(\S+)\s+(\S*)\s+(\S*)\s+((?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)\s+\w+\s+\d+\s+\d{2}:\d{2}:\d{2}\s+[+\-]\d{4}\s+\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<LinuxBinaryLogCollector> _logger;

    public string Name => "LinuxBinaryLog.Batch";

    public LinuxBinaryLogCollector(
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<LinuxBinaryLogCollector> logger)
    {
        _channel = channel;
        _dedupStore = dedupStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task CollectBatchAsync(CancellationToken cancellationToken)
    {
        // Run all three collectors in parallel for efficiency
        await Task.WhenAll(
            CollectWtmpAsync(cancellationToken),
            CollectBtmpAsync(cancellationToken),
            CollectLastlogAsync(cancellationToken));
    }

    // ── wtmp (successful logins/logouts/reboots) ─────────────────────────────

    private async Task CollectWtmpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lastTs = ParseTimestamp(await _dedupStore.GetCursorAsync(WtmpKey, cancellationToken));
            var output = await RunCommandAsync("last", new[] { "-F", "-n", "2000" }, cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
                return;

            var latest = lastTs;

            foreach (var line in output.Split('\n'))
            {
                var m = LastLineRegex.Match(line);
                if (!m.Success)
                    continue;

                var user = m.Groups[1].Value;
                var terminal = m.Groups[2].Value;
                var host = m.Groups[3].Value;
                var dateStr = m.Groups[4].Value.Trim();

                if (!TryParseLastDate(dateStr, out var ts))
                    continue;
                if (ts <= lastTs)
                    continue;

                var message = host.Length > 0
                    ? $"session: user={user} terminal={terminal} from={host} login={dateStr}"
                    : $"session: user={user} terminal={terminal} login={dateStr}";

                await _channel.Writer.WriteAsync(new LogEvent
                {
                    Timestamp = ts,
                    Hostname = Environment.MachineName,
                    Source = "wtmp",
                    Message = message,
                    Facility = SyslogFacility.Security,
                    Severity = SyslogSeverity.Informational,
                    CollectedBy = CollectionMode.Batch,
                    DeduplicationKey = $"wtmp:{ts:O}:{user}:{terminal}"
                }, cancellationToken);

                if (ts > latest)
                    latest = ts;
            }

            if (latest > lastTs)
                await _dedupStore.SetCursorAsync(WtmpKey, latest.ToString("O"), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting wtmp (last)");
        }
    }

    // ── btmp (failed login attempts) ─────────────────────────────────────────

    private async Task CollectBtmpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lastTs = ParseTimestamp(await _dedupStore.GetCursorAsync(BtmpKey, cancellationToken));
            // lastb requires root; if it fails we log a warning and skip gracefully
            var output = await RunCommandAsync("lastb", new[] { "-F", "-n", "2000" }, cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
                return;

            var latest = lastTs;

            foreach (var line in output.Split('\n'))
            {
                var m = LastLineRegex.Match(line);
                if (!m.Success)
                    continue;

                var user = m.Groups[1].Value;
                var terminal = m.Groups[2].Value;
                var host = m.Groups[3].Value;
                var dateStr = m.Groups[4].Value.Trim();

                if (!TryParseLastDate(dateStr, out var ts))
                    continue;
                if (ts <= lastTs)
                    continue;

                var message = host.Length > 0
                    ? $"failed-login: user={user} terminal={terminal} from={host} at={dateStr}"
                    : $"failed-login: user={user} terminal={terminal} at={dateStr}";

                await _channel.Writer.WriteAsync(new LogEvent
                {
                    Timestamp = ts,
                    Hostname = Environment.MachineName,
                    Source = "btmp",
                    Message = message,
                    Facility = SyslogFacility.Security,
                    Severity = SyslogSeverity.Warning,
                    CollectedBy = CollectionMode.Batch,
                    DeduplicationKey = $"btmp:{ts:O}:{user}:{terminal}"
                }, cancellationToken);

                if (ts > latest)
                    latest = ts;
            }

            if (latest > lastTs)
                await _dedupStore.SetCursorAsync(BtmpKey, latest.ToString("O"), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting btmp (lastb) — agent may need root for /var/log/btmp");
        }
    }

    // ── lastlog (last login timestamp per user) ──────────────────────────────

    private async Task CollectLastlogAsync(CancellationToken cancellationToken)
    {
        try
        {
            // lastlog output:
            // Username         Port     From             Latest
            // root             pts/0    192.168.1.1      Mon Jan 15 10:00:00 +0000 2024
            // daemon                                     **Never logged in**
            var output = await RunCommandAsync("lastlog", Array.Empty<string>(), cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
                return;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            // Skip header line
            var contentLines = lines.Length > 1 ? lines[1..] : lines;

            // Track per-user last-login date. Only emit an event when a specific
            // user's entry changes — a global hash would re-emit ALL users whenever
            // any single user logs in, flooding the SIEM with duplicate events.
            foreach (var line in contentLines)
            {
                if (line.Contains("**Never logged in**", StringComparison.OrdinalIgnoreCase))
                    continue;

                var m = LastlogLineRegex.Match(line);
                if (!m.Success)
                    continue;

                var user = m.Groups[1].Value;
                var port = m.Groups[2].Value;
                var from = m.Groups[3].Value;
                var dateStr = m.Groups[4].Value.Trim();

                // Per-user dedup key — only emit if this user's last-login date changed
                var userKey = $"binlog:lastlog:u:{user}";
                var storedDate = await _dedupStore.GetCursorAsync(userKey, cancellationToken);
                if (storedDate == dateStr)
                    continue;

                var message = from.Length > 0
                    ? $"last-login: user={user} port={port} from={from} at={dateStr}"
                    : $"last-login: user={user} port={port} at={dateStr}";

                await _channel.Writer.WriteAsync(new LogEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Hostname = Environment.MachineName,
                    Source = "lastlog",
                    Message = message,
                    Facility = SyslogFacility.Security,
                    Severity = SyslogSeverity.Informational,
                    CollectedBy = CollectionMode.Batch,
                    DeduplicationKey = $"lastlog:{user}:{dateStr}"
                }, cancellationToken);

                await _dedupStore.SetCursorAsync(userKey, dateStr, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting lastlog");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<string> RunCommandAsync(
        string command,
        string[] args,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };

        try
        {
            proc.Start();
        }
        catch (Exception)
        {
            // Command not found or not executable — skip silently
            return string.Empty;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);
            return output;
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(); } catch { /* already exited */ }
            return string.Empty;
        }
    }

    private static DateTimeOffset ParseTimestamp(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return DateTimeOffset.MinValue;
        return DateTimeOffset.TryParse(stored, out var dt) ? dt : DateTimeOffset.MinValue;
    }

    private static bool TryParseLastDate(string dateStr, out DateTimeOffset result)
    {
        // Normalize multiple spaces to single space (last -F pads with spaces)
        var normalized = Regex.Replace(dateStr, @"\s+", " ").Trim();

        return DateTimeOffset.TryParseExact(
            normalized,
            LastDateFormats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces,
            out result);
    }
}
