using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Logging;
using SyslogAgent;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Windows;

/// <summary>
/// Real-time Windows Event Log collector using EventLogWatcher.
/// Platforms: Windows only.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsEventLogCollector : IRealtimeCollector, IDisposable
{
    private readonly AgentOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<WindowsEventLogCollector> _logger;
    private readonly List<EventLogWatcher> _watchers = new();
    private readonly Dictionary<string, HashSet<int>> _filters = new();
    public string Name => "WindowsEventLog.Realtime";

    public WindowsEventLogCollector(
        AgentOptions options,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<WindowsEventLogCollector> logger)
    {
        _options = options;
        _channel = channel;
        _dedupStore = dedupStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Guard against being called twice on the same instance
        if (_watchers.Count > 0)
            return Task.CompletedTask;

        try
        {
            // Build per-channel Event ID filter lookup only when filtering is enabled
            if (_options.Collectors.ApplyFilter)
            {
                foreach (var kvp in _options.Collectors.WindowsEventLogFilter)
                {
                    _filters[kvp.Key] = new HashSet<int>(kvp.Value);
                }
            }

            foreach (var channelName in _options.Collectors.WindowsEventLogChannels)
            {
                // Always use wildcard XPath — filter by Event ID in OnEventRecordWritten callback.
                // Long XPath queries with many EventID conditions exceed the Windows API limit
                // and silently fail (e.g., Security channel with 48+ event IDs).
                try
                {
                    var query = new EventLogQuery(channelName, PathType.LogName, "*");
                    var watcher = new EventLogWatcher(query);
                    watcher.EventRecordWritten += OnEventRecordWritten;
                    watcher.Enabled = true;
                    _watchers.Add(watcher);

                    if (_options.Collectors.ApplyFilter && _filters.TryGetValue(channelName, out var logIds))
                        ConsoleWriter.WriteLine($"    Watching: {channelName} ({logIds.Count} event IDs filtered)", ConsoleColor.DarkGray);
                    else
                        ConsoleWriter.WriteLine($"    Watching: {channelName} (all events)", ConsoleColor.DarkGray);
                }
                catch (Exception)
                {
                    ConsoleWriter.WriteLine($"    Skipped:  {channelName} - not available on this system", ConsoleColor.DarkYellow);
                }
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WindowsEventLogCollector");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var watcher in _watchers)
        {
            watcher.Enabled = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        return Task.CompletedTask;
    }

    private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        try
        {
            if (e.EventRecord is null)
                return;

            using var record = e.EventRecord; // EventRecord is IDisposable

            // Filter by Event ID in code when ApplyFilter is enabled
            if (_options.Collectors.ApplyFilter &&
                record.LogName is not null &&
                _filters.TryGetValue(record.LogName, out var allowedIds) &&
                allowedIds.Count > 0 &&
                !allowedIds.Contains(record.Id))
            {
                // Event ID not in whitelist — skip but still advance offset
                // Block to ensure batch collector always sees the latest offset
                if (record.RecordId.HasValue)
                    _dedupStore.SetOffsetAsync(record.LogName, record.RecordId.Value, CancellationToken.None).GetAwaiter().GetResult();
                return;
            }

            var evt = WindowsEventLogMapper.Map(record, CollectionMode.Realtime);
            _channel.Writer.TryWrite(evt);

            // Advance the in-memory offset so the batch collector skips this event.
            // Block to ensure the offset is visible before the next batch cycle reads it.
            if (record.RecordId.HasValue && record.LogName is not null)
            {
                _dedupStore.SetOffsetAsync(record.LogName, record.RecordId.Value, CancellationToken.None).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event record");
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher?.Dispose();
        }
        _watchers.Clear();
    }
}
