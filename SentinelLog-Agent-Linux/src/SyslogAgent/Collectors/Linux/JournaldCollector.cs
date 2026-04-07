using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;

namespace SyslogAgent.Collectors.Linux;

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class JournaldCollector : IRealtimeCollector, IAsyncDisposable
{
    private readonly AgentOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<JournaldCollector> _logger;
    private Process? _journalProcess;
    private Task? _readTask;

    public string Name => "Journald.Realtime";

    public JournaldCollector(AgentOptions options, LogChannel channel, IDeduplicationStore dedupStore, ILogger<JournaldCollector> logger)
    {
        _options    = options;
        _channel    = channel;
        _dedupStore = dedupStore;
        _logger     = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var argList = BuildJournalctlArgList();
            _logger.LogInformation("Journald realtime starting: journalctl {Args}", string.Join(" ", argList));

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

            _journalProcess = new Process { StartInfo = psi };
            _journalProcess.Start();
            _readTask = Task.Run(() => ReadJournalctlOutputAsync(cancellationToken), cancellationToken);

            _logger.LogInformation("Journald collector started");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start JournaldCollector");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_journalProcess is not null)
        {
            try { _journalProcess.Kill(); } catch { }
            await _journalProcess.WaitForExitAsync(cancellationToken);
            _journalProcess.Dispose();
            _journalProcess = null;
        }

        if (_readTask is not null)
        {
            try { await _readTask; } catch { }
            _readTask = null;
        }
    }

    private List<string> BuildJournalctlArgList()
    {
        var args = new List<string> { "-f", "--output=json", "--since=now" };

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

    private async Task ReadJournalctlOutputAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_journalProcess?.StandardOutput is null)
                return;

            var stream = _journalProcess.StandardOutput;
            while (!stream.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await stream.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var evt = JournaldParser.Parse(line, CollectionMode.Realtime);
                if (evt is null)
                    continue;

                await _channel.Writer.WriteAsync(evt, cancellationToken);

                if (!string.IsNullOrEmpty(evt.DeduplicationKey))
                    await _dedupStore.SetCursorAsync("journald", evt.DeduplicationKey, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading journalctl output");
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_journalProcess is not null)
        {
            try
            {
                if (!_journalProcess.HasExited)
                {
                    _journalProcess.Kill();
                    await _journalProcess.WaitForExitAsync();
                }
            }
            catch { }
            _journalProcess.Dispose();
        }
    }
}
