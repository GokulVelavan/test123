using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;

namespace SyslogAgent.Collectors.Linux;

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class JournaldBatchCollector : IBatchCollector
{
    private readonly AgentOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<JournaldBatchCollector> _logger;

    public string Name => "Journald.Batch";

    public JournaldBatchCollector(
        AgentOptions options,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<JournaldBatchCollector> logger)
    {
        _options    = options;
        _channel    = channel;
        _dedupStore = dedupStore;
        _logger     = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken)  => Task.CompletedTask;

    public async Task CollectBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lastCursor = await _dedupStore.GetCursorAsync("journald", cancellationToken);

            if (lastCursor is not null && !JournaldParser.IsValidCursor(lastCursor))
            {
                _logger.LogWarning("Invalid journald cursor detected, ignoring: {Cursor}", lastCursor);
                lastCursor = null;
            }

            var argList = BuildBatchArgList(lastCursor);
            var output  = await RunJournalctlAsync(argList, cancellationToken);
            string? latestCursor = null;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var evt = JournaldParser.Parse(line, CollectionMode.Batch);
                if (evt is null)
                    continue;

                latestCursor = JournaldParser.ExtractCursor(line);
                await _channel.Writer.WriteAsync(evt, cancellationToken);
            }

            if (latestCursor is not null)
                await _dedupStore.SetCursorAsync("journald", latestCursor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in journald batch collection");
        }
    }

    private List<string> BuildBatchArgList(string? cursor)
    {
        var args = new List<string> { "--output=json" };

        if (cursor is not null)
            args.Add($"--after-cursor={cursor}");
        else
            args.Add("--boot");

        if (_options.Collectors.ApplyFilter)
        {
            var maxPriority = _options.Collectors.LinuxJournaldMaxPriority;
            if (maxPriority >= 0 && maxPriority < 7)
                args.Add($"--priority=0..{maxPriority}");

            foreach (var unit in _options.Collectors.LinuxJournaldUnits)
                args.Add($"--unit={unit}");

            foreach (var match in _options.Collectors.LinuxJournaldMatches)
            {
                if (!string.IsNullOrWhiteSpace(match) && JournaldParser.IsValidMatch(match))
                    args.Add(match);
                else
                    _logger.LogWarning("Skipping invalid journald match: {Match}", match);
            }
        }

        return args;
    }

    private static async Task<string> RunJournalctlAsync(List<string> argList, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName              = "journalctl",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute       = false,
            CreateNoWindow        = true
        };
        foreach (var arg in argList)
            psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // 60s timeout prevents a hung journalctl from blocking the batch cycle
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);
            return output;
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(); } catch { }
            return string.Empty;
        }
    }
}
