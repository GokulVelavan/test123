using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Linux;

/// <summary>
/// Batch collector for syslog files (/var/log/syslog, /var/log/messages, etc.).
/// Catches up missed events by reading from the last known offset.
/// </summary>
public sealed class SyslogFileBatchCollector : IBatchCollector
{
    private readonly string _filePath;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<SyslogFileBatchCollector> _logger;
    private readonly List<Regex> _patterns;
    private readonly bool _applyFilter;

    public string Name => $"SyslogFile.Batch:{_filePath}";

    public SyslogFileBatchCollector(
        string filePath,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        List<string> patterns,
        bool applyFilter,
        ILogger<SyslogFileBatchCollector> logger)
    {
        _filePath = filePath;
        _channel = channel;
        _dedupStore = dedupStore;
        _patterns = patterns.Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase)).ToList();
        _applyFilter = applyFilter;
        _logger = logger;
    }

    private bool MatchesFilter(string line)
    {
        if (!_applyFilter)
            return true;
        if (_patterns.Count == 0)
            return true;
        return _patterns.Any(p => p.IsMatch(line));
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
        try
        {
            if (!File.Exists(_filePath))
                return;

            var lastOffset = await _dedupStore.GetOffsetAsync(_filePath, cancellationToken);

            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Detect log rotation: if file is smaller than our offset, it was truncated/rotated
            if (stream.Length < lastOffset)
            {
                _logger.LogInformation("Log rotation detected for {FilePath}, resetting offset", _filePath);
                lastOffset = 0;
            }

            stream.Seek(lastOffset, SeekOrigin.Begin);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            long maxSeen = lastOffset;

            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    maxSeen = stream.Position;
                    continue;
                }

                if (!MatchesFilter(line))
                {
                    maxSeen = stream.Position;
                    continue;
                }

                var evt = new LogEvent
                {
                    Timestamp = DateTimeOffset.Now,
                    Hostname = Environment.MachineName,
                    Source = "syslog",
                    Message = line,
                    Facility = SyslogFacility.Local0,
                    Severity = SyslogSeverity.Informational,
                    CollectedBy = CollectionMode.Batch,
                    DeduplicationKey = $"{_filePath}:{maxSeen}"
                };

                await _channel.Writer.WriteAsync(evt, cancellationToken);
                maxSeen = stream.Position;
            }

            if (maxSeen > lastOffset)
            {
                await _dedupStore.SetOffsetAsync(_filePath, maxSeen, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch collection for syslog file: {FilePath}", _filePath);
        }
    }
}
