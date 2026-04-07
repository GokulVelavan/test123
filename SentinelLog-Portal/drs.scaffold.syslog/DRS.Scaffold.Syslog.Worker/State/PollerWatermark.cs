namespace DRS.Scaffold.Syslog.Worker.State;

/// <summary>
/// In-memory watermark that records the highest <c>systemlogs.id</c> already
/// processed by the poller. Seeded from and persisted to <c>platformconfig</c>
/// so restarts never reprocess old rows.
/// </summary>
public sealed class PollerWatermark
{
    public const string Category = "worker";
    public const string Key      = "poller.watermark";

    /// <summary>The last systemlogs row id that was fully processed.</summary>
    public int LastProcessedId { get; private set; } = 0;

    public void Advance(int newId)
    {
        if (newId > LastProcessedId)
            LastProcessedId = newId;
    }

    public void Seed(int persistedId) => LastProcessedId = persistedId;

    public void Reset() => LastProcessedId = 0;
}
