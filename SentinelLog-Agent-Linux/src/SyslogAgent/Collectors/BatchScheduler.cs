using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;

namespace SyslogAgent.Collectors;

/// <summary>
/// Periodically invokes all batch collectors to catch up missed events.
/// Also persists the deduplication store after each batch run.
/// </summary>
public sealed class BatchScheduler : BackgroundService
{
    private readonly IEnumerable<IBatchCollector> _batchCollectors;
    private readonly IDeduplicationStore _dedupStore;
    private readonly TimeSpan _interval;
    private readonly ILogger<BatchScheduler> _logger;

    public BatchScheduler(
        IEnumerable<IBatchCollector> batchCollectors,
        IDeduplicationStore dedupStore,
        IOptions<AgentOptions> options,
        ILogger<BatchScheduler> logger)
    {
        _batchCollectors = batchCollectors;
        _dedupStore = dedupStore;
        _interval = TimeSpan.FromSeconds(options.Value.BatchIntervalSeconds);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BatchScheduler started with {Interval} second interval", _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunBatchCollectorsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BatchScheduler stopping");
        }
        finally
        {
            // timer is disposed by 'using' — no need to call Dispose() again
            // Final persist on shutdown
            await _dedupStore.PersistAsync(CancellationToken.None);
        }
    }

    private async Task RunBatchCollectorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Persist first to flush any pending in-memory offset updates from
            // realtime collectors, so batch collectors read the latest offsets
            await _dedupStore.PersistAsync(cancellationToken);

            foreach (var collector in _batchCollectors)
            {
                try
                {
                    await collector.CollectBatchAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Batch collector {CollectorName} failed", collector.Name);
                }
            }

            // Persist state after all batch collectors complete
            await _dedupStore.PersistAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch scheduler");
        }
    }
}
