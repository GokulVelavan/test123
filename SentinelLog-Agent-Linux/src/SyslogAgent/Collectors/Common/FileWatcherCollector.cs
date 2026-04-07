using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Common;

/// <summary>
/// Real-time file watcher collector. Monitors files matching a pattern
/// and emits new lines as they are written.
/// </summary>
public sealed class FileWatcherCollector : IRealtimeCollector, IDisposable
{
    private readonly FileWatcherOptions _options;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<FileWatcherCollector> _logger;
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, long> _filePositions = new();

    public string Name => $"FileWatcher.Realtime:{_options.Path}";

    public FileWatcherCollector(
        FileWatcherOptions options,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<FileWatcherCollector> logger)
    {
        _options = options;
        _channel = channel;
        _dedupStore = dedupStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // _options.Path is the directory to watch (not a file path)
            var dirPath = _options.Path;
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
            {
                _logger.LogWarning("Directory not found: {Path}", dirPath);
                return;
            }

            // Initialize positions for existing files
            var files = Directory.GetFiles(dirPath, _options.Filter,
                _options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var offset = await _dedupStore.GetOffsetAsync(file, cancellationToken);
                _filePositions.TryAdd(file, offset);
            }

            _watcher = new FileSystemWatcher(_options.Path, _options.Filter)
            {
                IncludeSubdirectories = _options.Recursive,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (s, e) => OnFileChanged(e.FullPath);
            _watcher.Created += (s, e) => OnFileChanged(e.FullPath);

            _logger.LogInformation("FileWatcher started for {Path} (filter: {Filter})", _options.Path, _options.Filter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FileWatcherCollector");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;
        return Task.CompletedTask;
    }

    private void OnFileChanged(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var position = _filePositions.GetOrAdd(filePath, _ => 0);

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Detect log rotation: if file is smaller than our offset, it was truncated/rotated
            if (stream.Length < position)
            {
                _logger.LogInformation("Log rotation detected for {FilePath}, resetting offset", filePath);
                position = 0;
                _filePositions[filePath] = 0;
            }

            stream.Seek(position, SeekOrigin.Begin);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Use current stream position (after reading) for accurate dedup key
                var currentPosition = stream.Position;

                var evt = new LogEvent
                {
                    Timestamp = DateTimeOffset.Now,
                    Hostname = Environment.MachineName,
                    Source = Path.GetFileNameWithoutExtension(filePath),
                    Message = line,
                    Facility = _options.Facility,
                    Severity = _options.Severity,
                    CollectedBy = CollectionMode.Realtime,
                    DeduplicationKey = $"{filePath}:{currentPosition}"
                };

                _channel.Writer.TryWrite(evt);
            }

            // Update position after all lines are read
            var newPosition = stream.Position;
            _filePositions[filePath] = newPosition;
            // Block until the in-memory offset is updated so the batch collector
            // always sees the latest position and never re-sends lines we already sent.
            // SetOffsetAsync only touches an in-memory dictionary behind a brief semaphore,
            // so the blocking wait is negligible.
            _dedupStore.SetOffsetAsync(filePath, newPosition, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
