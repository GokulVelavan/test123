using System.Text;
using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;

namespace SyslogAgent.Collectors.Common;

/// <summary>
/// Batch file collector. Periodically reads files from the last known offset.
/// Catches up any missed events from real-time collector downtime.
/// </summary>
public sealed class FileBatchCollector : IBatchCollector
{
    private readonly FileWatcherOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<FileBatchCollector> _logger;

    public string Name => $"FileWatcher.Batch:{_options.Path}";

    public FileBatchCollector(
        FileWatcherOptions options,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<FileBatchCollector> logger)
    {
        _options = options;
        _channel = channel;
        _dedupStore = dedupStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Nothing to do on startup for batch collector
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
            // _options.Path is the directory to watch (not a file path)
            if (string.IsNullOrEmpty(_options.Path) || !Directory.Exists(_options.Path))
                return;

            var files = Directory.GetFiles(_options.Path, _options.Filter,
                _options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                await CollectFileAsync(filePath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch collection for {Path}", _options.Path);
        }
    }

    private async Task CollectFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var lastOffset = await _dedupStore.GetOffsetAsync(filePath, cancellationToken);

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Detect log rotation: if file is smaller than our offset, it was truncated/rotated
            if (stream.Length < lastOffset)
            {
                _logger.LogInformation("Log rotation detected for {FilePath}, resetting offset", filePath);
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

                var evt = new LogEvent
                {
                    Timestamp = DateTimeOffset.Now,
                    Hostname = Environment.MachineName,
                    Source = Path.GetFileNameWithoutExtension(filePath),
                    Message = line,
                    Facility = _options.Facility,
                    Severity = _options.Severity,
                    CollectedBy = CollectionMode.Batch,
                    DeduplicationKey = $"{filePath}:{maxSeen}"
                };

                await _channel.Writer.WriteAsync(evt, cancellationToken);
                maxSeen = stream.Position;
            }

            if (maxSeen > lastOffset)
            {
                await _dedupStore.SetOffsetAsync(filePath, maxSeen, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch for file: {FilePath}", filePath);
        }
    }
}
