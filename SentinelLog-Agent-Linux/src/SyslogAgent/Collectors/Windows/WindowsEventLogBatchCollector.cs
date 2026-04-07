using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Windows;

/// <summary>
/// Batch Windows Event Log collector using EventLogReader.
/// Catches up missed events by querying from the last known RecordId.
/// Platforms: Windows only.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsEventLogBatchCollector : IBatchCollector
{
    private readonly AgentOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<WindowsEventLogBatchCollector> _logger;

    public string Name => "WindowsEventLog.Batch";

    public WindowsEventLogBatchCollector(
        AgentOptions options,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<WindowsEventLogBatchCollector> logger)
    {
        _options = options;
        _channel = channel;
        _dedupStore = dedupStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task CollectBatchAsync(CancellationToken cancellationToken)
    {
        foreach (var channelName in _options.Collectors.WindowsEventLogChannels)
        {
            await CollectChannelAsync(channelName, cancellationToken);
        }
    }

    private readonly HashSet<string> _unavailableChannels = new();

    private async Task CollectChannelAsync(string channelName, CancellationToken cancellationToken)
    {
        // Skip channels we already know are unavailable
        if (_unavailableChannels.Contains(channelName))
            return;

        try
        {
            var lastId = await _dedupStore.GetOffsetAsync(channelName, cancellationToken);

            // First run (no stored offset): seed to the current latest RecordId
            // so we only collect NEW events going forward, not the entire history.
            if (lastId == 0)
            {
                lastId = GetLatestRecordId(channelName);
                if (lastId > 0)
                {
                    await _dedupStore.SetOffsetAsync(channelName, lastId, cancellationToken);
                    _logger.LogInformation("First run — seeded {Channel} offset to latest RecordId {Id} (skipping history)",
                        channelName, lastId);
                }
                return; // Nothing to catch up on first run
            }

            // Build XPath with RecordID offset — filter Event IDs in code to avoid
            // XPath query length limits that cause "The specified query is invalid"
            var xpath = $"*[System[(EventRecordID > {lastId})]]";

            // Get the whitelist for this channel (only when filtering is enabled)
            HashSet<int>? allowedIds = null;
            if (_options.Collectors.ApplyFilter &&
                _options.Collectors.WindowsEventLogFilter.TryGetValue(channelName, out var ids) && ids.Count > 0)
            {
                allowedIds = new HashSet<int>(ids);
            }

            var query = new EventLogQuery(channelName, PathType.LogName, xpath);

            using var reader = new EventLogReader(query);
            long maxSeen = lastId;

            EventRecord? record;
            while ((record = reader.ReadEvent()) is not null)
            {
                using (record)
                {
                    if (record.RecordId.HasValue && record.RecordId > maxSeen)
                        maxSeen = record.RecordId.Value;

                    // Filter by Event ID in code if whitelist is configured
                    if (allowedIds is not null && record.Id > 0 && !allowedIds.Contains(record.Id))
                        continue;

                    var evt = WindowsEventLogMapper.Map(record, CollectionMode.Batch);
                    await _channel.Writer.WriteAsync(evt, cancellationToken);
                }
            }

            if (maxSeen > lastId)
            {
                await _dedupStore.SetOffsetAsync(channelName, maxSeen, cancellationToken);
            }
        }
        catch (EventLogNotFoundException)
        {
            _logger.LogWarning("Channel {Channel} not found on this system — skipping", channelName);
            _unavailableChannels.Add(channelName);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied for channel {Channel} — run as Administrator", channelName);
            _unavailableChannels.Add(channelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting batch from channel: {Channel}", channelName);
        }
    }

    /// <summary>
    /// Reads the most recent RecordId from the given channel so we can skip all historical events.
    /// </summary>
    private long GetLatestRecordId(string channelName)
    {
        try
        {
            // Query in reverse chronological order, read just the first (latest) event
            var query = new EventLogQuery(channelName, PathType.LogName, "*")
            {
                ReverseDirection = true
            };
            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();
            return record?.RecordId ?? 0;
        }
        catch (EventLogNotFoundException)
        {
            _unavailableChannels.Add(channelName);
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            _unavailableChannels.Add(channelName);
            return 0;
        }
        catch (Exception)
        {
            _unavailableChannels.Add(channelName);
            return 0;
        }
    }

}
