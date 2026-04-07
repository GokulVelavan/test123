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
/// Batch collector for the kernel ring buffer via `dmesg`.
/// Useful on systems where journald is not running (Alpine, minimal containers, etc.).
/// On systems with journald, the JournaldCollector already captures kernel messages
/// (facility=Kernel) — enable this only when journald is absent or as a fallback.
///
/// Uses `dmesg --time-format=iso` to get ISO-8601 timestamps and tracks the latest
/// seen timestamp in the deduplication store so each batch run only emits new entries.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class DmesgBatchCollector : IBatchCollector
{
    private const string DedupKey = "dmesg:last_timestamp";

    // Matches: [2024-01-15T10:23:45,123456+00:00] message text
    // The level prefix (e.g. "kern  :err : ") from `dmesg -x` is optional.
    private static readonly Regex IsoLineRegex = new(
        @"^\s*(?:[a-z]+\s*:[a-z]+\s*:\s*)?\[(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2},\d+[+\-]\d{2}:\d{2})\]\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches: [    1.234567] message — fallback when no real-time clock at boot
    private static readonly Regex MonotonicLineRegex = new(
        @"^\s*\[\s*(\d+\.\d+)\]\s*(.+)$",
        RegexOptions.Compiled);

    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<DmesgBatchCollector> _logger;

    public string Name => "Dmesg.Batch";

    public DmesgBatchCollector(
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<DmesgBatchCollector> logger)
    {
        _channel = channel;
        _dedupStore = dedupStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task CollectBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Restore last seen timestamp from durable store
            var storedCursor = await _dedupStore.GetCursorAsync(DedupKey, cancellationToken);
            var lastSeenTimestamp = ParseStoredTimestamp(storedCursor);

            var output = await RunDmesgAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
                return;

            var hostname = Environment.MachineName;
            DateTimeOffset latestSeen = lastSeenTimestamp;
            bool useMonotonic = false;
            double lastMonotonicSecs = TryParseMonotonicCursor(storedCursor);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Try ISO timestamp format first
                var isoMatch = IsoLineRegex.Match(line);
                if (isoMatch.Success)
                {
                    if (!DateTimeOffset.TryParse(isoMatch.Groups[1].Value, out var ts))
                        continue;

                    if (ts <= lastSeenTimestamp)
                        continue;

                    var message = isoMatch.Groups[2].Value.Trim();
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    await _channel.Writer.WriteAsync(BuildEvent(hostname, ts, message), cancellationToken);

                    if (ts > latestSeen)
                        latestSeen = ts;

                    continue;
                }

                // Fallback: monotonic timestamp (systems that booted before RTC sync)
                var monMatch = MonotonicLineRegex.Match(line);
                if (monMatch.Success)
                {
                    useMonotonic = true;
                    if (!double.TryParse(monMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var mono))
                        continue;

                    if (mono <= lastMonotonicSecs)
                        continue;

                    var message = monMatch.Groups[2].Value.Trim();
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    // Use current time as approximate timestamp when no wall-clock available
                    var approxTs = DateTimeOffset.UtcNow;
                    await _channel.Writer.WriteAsync(BuildEvent(hostname, approxTs, message), cancellationToken);

                    if (mono > lastMonotonicSecs)
                        lastMonotonicSecs = mono;
                }
            }

            // Persist latest seen marker
            if (useMonotonic && lastMonotonicSecs > TryParseMonotonicCursor(storedCursor))
            {
                await _dedupStore.SetCursorAsync(DedupKey,
                    $"mono:{lastMonotonicSecs.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    cancellationToken);
            }
            else if (!useMonotonic && latestSeen > lastSeenTimestamp)
            {
                await _dedupStore.SetCursorAsync(DedupKey, latestSeen.ToString("O"), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in dmesg batch collection");
        }
    }

    private static LogEvent BuildEvent(string hostname, DateTimeOffset timestamp, string message) =>
        new()
        {
            Timestamp = timestamp,
            Hostname = hostname,
            Source = "kernel",
            Message = message,
            Facility = SyslogFacility.Kernel,
            Severity = ClassifyKernelSeverity(message),
            CollectedBy = CollectionMode.Batch,
            DeduplicationKey = $"dmesg:{timestamp:O}:{message.GetHashCode()}"
        };

    private static SyslogSeverity ClassifyKernelSeverity(string message)
    {
        // dmesg with -x prefixes like "kern  :err : [...]" but we already stripped it.
        // Fall back to keyword classification on the message text.
        var m = message.AsSpan();
        if (ContainsIgnoreCase(m, "panic") || ContainsIgnoreCase(m, "oops"))
            return SyslogSeverity.Critical;
        if (ContainsIgnoreCase(m, "error") || ContainsIgnoreCase(m, "fault") || ContainsIgnoreCase(m, "failed"))
            return SyslogSeverity.Error;
        if (ContainsIgnoreCase(m, "warning") || ContainsIgnoreCase(m, "warn"))
            return SyslogSeverity.Warning;
        return SyslogSeverity.Informational;
    }

    private static bool ContainsIgnoreCase(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle) =>
        haystack.ToString().IndexOf(needle.ToString(), StringComparison.OrdinalIgnoreCase) >= 0;

    private static DateTimeOffset ParseStoredTimestamp(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.StartsWith("mono:", StringComparison.Ordinal))
            return DateTimeOffset.MinValue;
        return DateTimeOffset.TryParse(cursor, out var dt) ? dt : DateTimeOffset.MinValue;
    }

    private static double TryParseMonotonicCursor(string? cursor)
    {
        if (cursor is null || !cursor.StartsWith("mono:", StringComparison.Ordinal))
            return 0.0;
        var raw = cursor["mono:".Length..];
        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0;
    }

    private static async Task<string> RunDmesgAsync(CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dmesg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // --time-format=iso gives ISO-8601 wall-clock timestamps (requires util-linux >= 2.24)
        psi.ArgumentList.Add("--time-format=iso");

        using var proc = new Process { StartInfo = psi };

        try
        {
            proc.Start();
        }
        catch (Exception)
        {
            // dmesg not available or permission denied — return empty
            return string.Empty;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

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
}
