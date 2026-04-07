namespace DRS.Scaffold.Syslog.Worker.Metrics;

/// <summary>
/// Thread-safe accumulator for within-flush-interval metrics.
/// Counters are reset each time <see cref="Drain"/> is called.
/// </summary>
public sealed class MetricAccumulator
{
    private long _eventsProcessed;
    private long _alertsFired;
    private readonly long[] _severityCounts = new long[8]; // index = RFC-5424 numeric severity
    private readonly object _lock = new();

    public void RecordEvent(short? severity)
    {
        Interlocked.Increment(ref _eventsProcessed);
        if (severity is >= 0 and <= 7)
            Interlocked.Increment(ref _severityCounts[severity.Value]);
    }

    public void RecordAlert() => Interlocked.Increment(ref _alertsFired);

    /// <summary>
    /// Atomically reads all counters and resets them to zero,
    /// returning a snapshot suitable for writing to <c>systemmetric</c>.
    /// </summary>
    public MetricSnapshot Drain()
    {
        lock (_lock)
        {
            var snapshot = new MetricSnapshot
            {
                EventsProcessed  = Interlocked.Exchange(ref _eventsProcessed, 0),
                AlertsFired      = Interlocked.Exchange(ref _alertsFired, 0),
                SeverityCounts   = _severityCounts.Select(
                    (_, i) => Interlocked.Exchange(ref _severityCounts[i], 0)).ToArray()
            };
            return snapshot;
        }
    }
}

/// <summary>Immutable snapshot produced by <see cref="MetricAccumulator.Drain"/>.</summary>
public sealed class MetricSnapshot
{
    public long   EventsProcessed  { get; init; }
    public long   AlertsFired      { get; init; }
    public long[] SeverityCounts   { get; init; } = new long[8];
}
