namespace SyslogAgent.Collectors;

/// <summary>
/// Base interface for log collectors (real-time and batch).
/// </summary>
public interface ILogCollector
{
    /// <summary>
    /// Human-readable name of the collector.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Starts the collector (e.g., registers watchers, opens connections).
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the collector gracefully.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A collector that continuously monitors for new log events.
/// </summary>
public interface IRealtimeCollector : ILogCollector
{
    // Implementations push LogEvent objects to the LogChannel.Writer as events arrive.
}

/// <summary>
/// A collector that periodically reads logs from a known offset.
/// Catches up any missed events (e.g., during downtime).
/// </summary>
public interface IBatchCollector : ILogCollector
{
    /// <summary>
    /// Collects logs from the last known offset and emits them.
    /// </summary>
    Task CollectBatchAsync(CancellationToken cancellationToken);
}
