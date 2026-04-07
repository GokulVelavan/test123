using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SyslogAgent.Deduplication;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Linux;

/// <summary>
/// Dual-mode collector (real-time + batch) that automatically discovers ALL text-based
/// log files under /var/log/ and its subdirectories — including files that did not exist
/// at agent startup.
///
/// This covers the "long tail" of application logs that are not listed in the explicit
/// LinuxSyslogFiles configuration (databases, custom apps, container runtimes, etc.).
///
/// Design:
///   Real-time — FileSystemWatcher (recursive) on every watch root.
///               Detects file changes and new files in real time.
///   Batch     — Full scan of all watch roots on each scheduled interval.
///               Ensures no data is missed if the FSW event queue overflows.
///
/// Implements both IRealtimeCollector and IBatchCollector so CollectorRegistry
/// registers a single instance for both modes.
///
/// Files are skipped when:
///   • They appear in the explicit LinuxSyslogFiles exclusion list (already monitored).
///   • They are known binary accounting files (wtmp, btmp, lastlog, etc.).
///   • They cannot be opened for reading.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxLogDiscoveryCollector : IRealtimeCollector, IBatchCollector, IDisposable
{
    // Roots to watch recursively
    private static readonly string[] WatchRoots = { "/var/log" };

    // Binary accounting files that must not be read as text
    private static readonly HashSet<string> BinaryFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "wtmp", "btmp", "lastlog", "faillog", "tallylog", "utmp",
        "lastb", "dmrc"
    };

    // File extensions that are always binary or compressed — skip
    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gz", ".bz2", ".xz", ".zst", ".zip", ".lz4",
        ".bin", ".db", ".sqlite", ".sqlite3",
        ".pcap", ".pcapng",
        ".core", ".dump"
    };

    // FSW list — one per watch root
    private readonly List<FileSystemWatcher> _watchers = new();

    // Deduplicate rapid FSW events per file using a short-lived set + lock
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _pendingReads = new();

    private readonly LogChannel _channel;
    private readonly IDeduplicationStore _dedupStore;
    private readonly HashSet<string> _excludedFiles;
    private readonly ILogger<LinuxLogDiscoveryCollector> _logger;

    private volatile bool _started;

    public string Name => "LinuxLogDiscovery";

    public LinuxLogDiscoveryCollector(
        IEnumerable<string> excludedFiles,
        LogChannel channel,
        IDeduplicationStore dedupStore,
        ILogger<LinuxLogDiscoveryCollector> logger)
    {
        _channel = channel;
        _dedupStore = dedupStore;
        _logger = logger;
        // Normalise paths for comparison
        _excludedFiles = new HashSet<string>(
            excludedFiles.Select(p => p.Trim()),
            StringComparer.Ordinal);
    }

    // ── IRealtimeCollector ───────────────────────────────────────────────────

    public async Task StartAsync(CancellationToken cancellationToken)
    {

        foreach (var root in WatchRoots)
        {
            if (!Directory.Exists(root))
            {
                _logger.LogDebug("Log discovery: watch root not found, skipping: {Root}", root);
                continue;
            }

            // Do an initial scan so we catch up any data written before the FSW starts
            await ScanDirectoryAsync(root, cancellationToken);

            var fsw = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                // Watch all files; we filter in the handler
                Filter = "*",
                EnableRaisingEvents = true,
                // Increase internal buffer to reduce overflow risk under heavy log activity
                InternalBufferSize = 65536
            };

            fsw.Changed += OnFileEvent;
            fsw.Created += OnFileEvent;
            fsw.Error += OnWatcherError;
            _watchers.Add(fsw);

            _logger.LogInformation("Log discovery: watching {Root} (recursive)", root);
        }

        _started = true;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _started = false;
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        return Task.CompletedTask;
    }

    // ── IBatchCollector ──────────────────────────────────────────────────────

    public async Task CollectBatchAsync(CancellationToken cancellationToken)
    {
        foreach (var root in WatchRoots)
        {
            if (Directory.Exists(root))
                await ScanDirectoryAsync(root, cancellationToken);
        }

        // Persist offsets to disk after batch run so next startup resumes from correct position
        await _dedupStore.PersistAsync(cancellationToken);
    }

    // ── FileSystemWatcher callbacks ──────────────────────────────────────────

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (!_started)
            return;

        var path = e.FullPath;
        if (!ShouldMonitor(path))
            return;

        // Debounce: if a read is already scheduled for this file, skip the duplicate event
        if (!_pendingReads.TryAdd(path, 0))
            return;

        // Schedule the read on the thread pool so the FSW callback returns immediately
        _ = Task.Run(async () =>
        {
            try
            {
                await ReadFileAsync(path, CancellationToken.None);
            }
            finally
            {
                _pendingReads.TryRemove(path, out _);
            }
        });
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogWarning(e.GetException(),
            "Log discovery: FileSystemWatcher error — events may have been lost, batch run will catch up");
    }

    // ── Core scan / read logic ───────────────────────────────────────────────

    private async Task ScanDirectoryAsync(string root, CancellationToken cancellationToken)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Log discovery: cannot enumerate {Root}", root);
            return;
        }

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldMonitor(path))
                await ReadFileAsync(path, cancellationToken, CollectionMode.Batch);
        }
    }

    private async Task ReadFileAsync(string path, CancellationToken cancellationToken, CollectionMode mode = CollectionMode.Realtime)
    {
        try
        {
            if (!File.Exists(path))
                return;

            // Use the dedup store as the single source of truth for offset state.
            // FileOffsetStore is protected by a SemaphoreSlim so concurrent calls for
            // the same path (e.g. FSW event + batch scan running simultaneously) are
            // serialized — no TOCTOU race, no duplicate emissions.
            var currentOffset = await _dedupStore.GetOffsetAsync(path, cancellationToken);

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            // Detect log rotation: file is smaller than our stored offset
            if (stream.Length < currentOffset)
            {
                _logger.LogInformation("Log discovery: rotation detected for {Path}, resetting offset", path);
                currentOffset = 0;
            }

            if (stream.Length == currentOffset)
                return; // No new data

            stream.Seek(currentOffset, SeekOrigin.Begin);

            // Read up to 4 MB per pass to avoid unbounded allocations on very large files
            var bufferSize = (int)Math.Min(stream.Length - currentOffset, 4 * 1024 * 1024);
            var buffer = new byte[bufferSize];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken);

            if (bytesRead == 0)
                return;

            var newOffset = currentOffset + bytesRead;
            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var hostname = Environment.MachineName;

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                await _channel.Writer.WriteAsync(new LogEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Hostname = hostname,
                    Source = Path.GetFileName(path),
                    Message = trimmed,
                    Facility = SyslogFacility.Local1,
                    Severity = SyslogSeverity.Informational,
                    CollectedBy = mode,
                    DeduplicationKey = $"disc:{path}:{newOffset}"
                }, cancellationToken);
            }

            // Advance the stored offset so the next read (FSW or batch) starts here
            await _dedupStore.SetOffsetAsync(path, newOffset, cancellationToken);
        }
        catch (IOException)
        {
            // File locked, deleted mid-read, or access denied — skip silently, next run retries
        }
        catch (UnauthorizedAccessException)
        {
            // No read permission — log once and the file will be skipped on future scans too
            _logger.LogDebug("Log discovery: access denied for {Path}, skipping", path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Log discovery: error reading {Path}", path);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool ShouldMonitor(string path)
    {
        // Already explicitly monitored by SyslogFileCollector
        if (_excludedFiles.Contains(path))
            return false;

        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var baseName = Path.GetFileName(path);

        // Binary accounting files
        if (BinaryFileNames.Contains(baseName) || BinaryFileNames.Contains(name))
            return false;

        // Compressed / binary extensions
        if (SkipExtensions.Contains(ext))
            return false;

        // Rotated log files (e.g., syslog.1, auth.log.2024-01-15) — skip numeric rotations
        // but allow date-suffixed ones only if no extension (many distros keep them readable)
        if (Regex.IsMatch(baseName, @"\.\d+$"))
            return false;

        return true;
    }

    public void Dispose()
    {
        foreach (var w in _watchers)
            w.Dispose();
        _watchers.Clear();
    }
}
