namespace SyslogAgent.Collectors;

/// <summary>
/// Holds the shared list of all collectors created by the factory.
/// Ensures a single materialized list is reused across all DI resolutions.
/// </summary>
public sealed class CollectorRegistry
{
    public IReadOnlyList<ILogCollector> All { get; }
    public IReadOnlyList<IBatchCollector> BatchCollectors { get; }

    public CollectorRegistry(List<ILogCollector> collectors)
    {
        All = collectors;
        BatchCollectors = collectors.OfType<IBatchCollector>().ToList();
    }
}
