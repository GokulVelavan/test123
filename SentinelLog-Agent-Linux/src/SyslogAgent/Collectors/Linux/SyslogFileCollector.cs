using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Linux;

/// <summary>
/// Real-time syslog file collector for /var/log/*.
/// Monitors /var/log/syslog, /var/log/messages, etc. using FileSystemWatcher.
/// Platforms: Linux (works on any OS with these files).
/// </summary>
public sealed class SyslogFileCollector : IRealtimeCollector, IDisposable
{
    private readonly string _filePath;
    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly ILogger<SyslogFileCollector> _logger;
    private readonly List<Regex> _patterns;
    private readonly bool _applyFilter;
    private long _filePosition;
    private FileSystemWatcher? _watcher;

    public string Name => $"SyslogFile.Realtime:{_filePath}";

    public SyslogFileCollector(
        string filePath,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        List<string> patterns,
        bool applyFilter,
        ILogger<SyslogFileCollector> logger)
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
        // When filtering is disabled, accept all lines
        if (!_applyFilter)
            return true;
        // No patterns = accept all lines
        if (_patterns.Count == 0)
            return true;
        return _patterns.Any(p => p.IsMatch(line));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("Syslog file not found: {FilePath}", _filePath);
                return;
            }

            // Load initial position from store
            _filePosition = await _dedupStore.GetOffsetAsync(_filePath, cancellationToken);

            var dirPath = Path.GetDirectoryName(_filePath);
            var fileName = Path.GetFileName(_filePath);

            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
            {
                _logger.LogWarning("Directory not found for syslog file: {FilePath}", _filePath);
                return;
            }

            _watcher = new FileSystemWatcher(dirPath, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (s, e) => OnFileChanged();

            _logger.LogInformation("Started watching syslog file: {FilePath}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SyslogFileCollector for {FilePath}", _filePath);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;
        return Task.CompletedTask;
    }

    private void OnFileChanged()
    {
        try
        {
            if (!File.Exists(_filePath))
                return;

            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Detect log rotation: if file is smaller than our offset, it was truncated/rotated
            if (stream.Length < _filePosition)
            {
                _logger.LogInformation("Log rotation detected for {FilePath}, resetting offset", _filePath);
                _filePosition = 0;
            }

            stream.Seek(_filePosition, SeekOrigin.Begin);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!MatchesFilter(line))
                    continue;

                // Use current stream position (after reading) for accurate dedup key
                var currentPosition = stream.Position;

                var evt = new LogEvent
                {
                    Timestamp = DateTimeOffset.Now,
                    Hostname = Environment.MachineName,
                    Source = "syslog",
                    Message = line,
                    Facility = SyslogFacility.Local0,
                    Severity = SyslogSeverity.Informational,
                    CollectedBy = CollectionMode.Realtime,
                    DeduplicationKey = $"{_filePath}:{currentPosition}"
                };

                _channel.Writer.TryWrite(evt);
            }

            _filePosition = stream.Position;
            // Block until the in-memory offset is updated so the batch collector
            // always sees the latest position and never re-sends lines we already sent.
            // SetOffsetAsync only touches an in-memory dictionary behind a brief semaphore,
            // so the blocking wait is negligible.
            _dedupStore.SetOffsetAsync(_filePath, _filePosition, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing syslog file: {FilePath}", _filePath);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
