namespace SyslogAgent.Deduplication;

/// <summary>
/// Stores offsets and cursors to track which logs have been sent,
/// enabling deduplication between real-time and batch collectors.
/// </summary>
public interface IDeduplicationStore
{
    /// <summary>
    /// Gets the byte offset for a given key (e.g., file path).
    /// Returns 0 if not found (start from beginning).
    /// </summary>
    Task<long> GetOffsetAsync(string key, CancellationToken ct);

    /// <summary>
    /// Sets the byte offset for a given key.
    /// </summary>
    Task SetOffsetAsync(string key, long offset, CancellationToken ct);

    /// <summary>
    /// Gets the cursor string for a given key (e.g., journald __CURSOR).
    /// Returns null if not found.
    /// </summary>
    Task<string?> GetCursorAsync(string key, CancellationToken ct);

    /// <summary>
    /// Sets the cursor string for a given key.
    /// </summary>
    Task SetCursorAsync(string key, string cursor, CancellationToken ct);

    /// <summary>
    /// Persists all in-memory state to durable storage.
    /// </summary>
    Task PersistAsync(CancellationToken ct);
}
