using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyslogAgent;

namespace SyslogAgent.Collectors;

/// <summary>
/// Manages the lifecycle of all log collectors.
/// Starts them on application startup and gracefully stops them on shutdown.
/// </summary>
public sealed class CollectorHost : BackgroundService
{
    private readonly IEnumerable<ILogCollector> _collectors;
    private readonly ILogger<CollectorHost> _logger;

    public CollectorHost(IEnumerable<ILogCollector> collectors, ILogger<CollectorHost> logger)
    {
        _collectors = collectors;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CollectorHost starting all collectors");

        try
        {
            foreach (var collector in _collectors)
            {
                try
                {
                    await collector.StartAsync(stoppingToken);
                    ConsoleWriter.WriteLine($"  Started: {collector.Name}", ConsoleColor.DarkGreen);
                }
                catch (Exception ex)
                {
                    ConsoleWriter.WriteLine($"  Failed:  {collector.Name} - {ex.Message}", ConsoleColor.Red);
                    _logger.LogError(ex, "Failed to start collector: {CollectorName}", collector.Name);
                }
            }

            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CollectorHost stopping");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CollectorHost stopping all collectors");

        foreach (var collector in _collectors.Reverse())
        {
            try
            {
                await collector.StopAsync(cancellationToken);
                _logger.LogInformation("Stopped collector: {CollectorName}", collector.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping collector: {CollectorName}", collector.Name);
            }
        }

        await base.StopAsync(cancellationToken);
    }
}
