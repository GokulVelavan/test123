using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyslogAgent.Configuration;

namespace SyslogAgent.Deduplication;

/// <summary>
/// Persists offsets and cursors to a JSON file.
/// Thread-safe via SemaphoreSlim.
/// </summary>
public sealed class FileOffsetStore : IDeduplicationStore, IAsyncDisposable
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<FileOffsetStore> _logger;
    private Dictionary<string, long> _offsets = new();
    private Dictionary<string, string> _cursors = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FileOffsetStore(IOptions<AgentOptions> options, ILogger<FileOffsetStore> logger)
    {
        var configuredPath = options.Value.DeduplicationStorePath;
        // Resolve relative paths against AppContext.BaseDirectory so the store file
        // is always beside the executable, not dependent on the working directory
        _filePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);
        _logger = logger;
    }

    /// <summary>
    /// Initializes the store by loading from disk if it exists.
    /// Should be called before starting collectors.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath, ct);
                var doc = JsonSerializer.Deserialize<OffsetDocument>(json, JsonOptions);
                if (doc is not null)
                {
                    _offsets = doc.Offsets ?? new();
                    _cursors = doc.Cursors ?? new();
                    _logger.LogInformation("Deduplication store initialized: {OffsetCount} offsets, {CursorCount} cursors",
                        _offsets.Count, _cursors.Count);
                }
            }
            else
            {
                _logger.LogInformation("Deduplication store file not found, starting fresh");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize deduplication store from {Path}", _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<long> GetOffsetAsync(string key, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _offsets.TryGetValue(key, out var offset) ? offset : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetOffsetAsync(string key, long offset, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Only advance forward — prevents batch collectors from writing back
            // a stale (lower) offset over one that realtime already advanced
            if (!_offsets.TryGetValue(key, out var existing) || offset > existing)
                _offsets[key] = offset;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetCursorAsync(string key, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _cursors.TryGetValue(key, out var cursor) ? cursor : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetCursorAsync(string key, string cursor, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _cursors[key] = cursor;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PersistAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var doc = new OffsetDocument
            {
                Offsets = new Dictionary<string, long>(_offsets),
                Cursors = new Dictionary<string, string>(_cursors)
            };

            var json = JsonSerializer.Serialize(doc, JsonOptions);

            // Atomic write: write to temp file, then move to target.
            // This prevents corruption if the process crashes mid-write.
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, _filePath, overwrite: true);
            _logger.LogDebug("Deduplication store persisted to {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist deduplication store to {Path}", _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _lock?.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// DTO for JSON serialization.
    /// </summary>
    private sealed class OffsetDocument
    {
        [JsonPropertyName("offsets")]
        public Dictionary<string, long> Offsets { get; set; } = new();

        [JsonPropertyName("cursors")]
        public Dictionary<string, string> Cursors { get; set; } = new();
    }
}
