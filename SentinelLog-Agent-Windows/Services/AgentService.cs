using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SyslogAgent.Collectors;
using SyslogAgent.Configuration;
using SyslogAgent.Deduplication;
using SyslogAgent.Desktop.Models;
using SyslogAgent.Models;
using SyslogAgent.Pipeline;
using SyslogAgent.Syslog;

namespace SyslogAgent.Desktop.Services;

public sealed class AgentService : IDisposable
{
    private readonly AgentOptions _options;
    private readonly LogChannel _channel;
    private readonly SyslogSender _sender;
    private readonly FileOffsetStore _dedupStore;
    private readonly CollectorRegistry _registry;
    private readonly ILoggerFactory _loggerFactory;
    private CancellationTokenSource? _cts;
    private Task? _pipelineTask;
    private Task? _batchTask;
    private readonly DateTime _startTime = DateTime.UtcNow;

    private long _eventsSent;
    private long _sendErrors;

    public event Action<LogEntryVm>? LogReceived;
    public event Action<long, long>? MetricsUpdated;

    public AgentOptions Options => _options;
    public CollectorRegistry Registry => _registry;
    public long EventsSent => Interlocked.Read(ref _eventsSent);
    public long SendErrors => Interlocked.Read(ref _sendErrors);
    public TimeSpan Uptime => DateTime.UtcNow - _startTime;

    public AgentService(string host, int port, string protocol, bool applyFilter)
    {
        _options = LoadOptions(host, port, protocol, applyFilter);
        _loggerFactory = LoggerFactory.Create(_ => { }); // silent loggers

        _channel = new LogChannel(_options.ChannelCapacity);

        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);
        _dedupStore = new FileOffsetStore(optionsWrapper, _loggerFactory.CreateLogger<FileOffsetStore>());
        _sender = new SyslogSender(optionsWrapper, _loggerFactory.CreateLogger<SyslogSender>());

        var collectors = CollectorFactory.CreateCollectors(
            _options, _channel, _dedupStore, _loggerFactory).ToList();
        _registry = new CollectorRegistry(collectors);
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();

        await _dedupStore.InitializeAsync(_cts.Token);

        foreach (var collector in _registry.All)
        {
            try
            {
                await collector.StartAsync(_cts.Token);
            }
            catch
            {
                // Channel not available on this system — skip silently
            }
        }

        _pipelineTask = Task.Run(() => RunPipelineAsync(_cts.Token));
        _batchTask    = Task.Run(() => RunBatchSchedulerAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        _cts.Cancel();

        // Stop collectors so no new events enter the channel
        foreach (var collector in _registry.All.Reverse())
        {
            try { await collector.StopAsync(CancellationToken.None); }
            catch { }
        }

        // Wait for the pipeline task to exit (it exits when ct is cancelled)
        if (_pipelineTask is not null)
            try { await _pipelineTask; } catch { }

        // Complete the writer so ReadAllAsync terminates, then drain any buffered events
        // that were queued before cancellation — ensures no data loss on graceful stop
        _channel.Writer.TryComplete();
        await DrainRemainingAsync();

        // Wait for batch scheduler to exit
        if (_batchTask is not null)
            try { await _batchTask; } catch { }

        await _dedupStore.PersistAsync(CancellationToken.None);
    }

    private async Task DrainRemainingAsync()
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                try
                {
                    await _sender.SendAsync(evt, CancellationToken.None);
                    Interlocked.Increment(ref _eventsSent);
                }
                catch
                {
                    Interlocked.Increment(ref _sendErrors);
                }
            }
        }
        catch { }
    }

    private async Task RunPipelineAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await _sender.SendAsync(evt, ct);
                    Interlocked.Increment(ref _eventsSent);

                    var severityStr = evt.Severity.ToString();
                    var color = severityStr switch
                    {
                        "Emergency" or "Alert" or "Critical" or "Error" => "#DC2626",
                        "Warning" => "#D97706",
                        "Notice" => "#2563EB",
                        "Informational" => "#059669",
                        "Debug" => "#6B7280",
                        _ => "#333333"
                    };

                    var msg = evt.Message.Length > 200
                        ? evt.Message[..197] + "..."
                        : evt.Message;

                    var entry = new LogEntryVm
                    {
                        Time = evt.Timestamp.ToString("HH:mm:ss.fff"),
                        Source = evt.Source,
                        Facility = evt.Facility.ToString(),
                        Severity = severityStr,
                        Message = msg.ReplaceLineEndings(" "),
                        SeverityColor = color
                    };

                    LogReceived?.Invoke(entry);
                    MetricsUpdated?.Invoke(EventsSent, SendErrors);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    Interlocked.Increment(ref _sendErrors);
                    MetricsUpdated?.Invoke(EventsSent, SendErrors);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunBatchSchedulerAsync(CancellationToken ct)
    {
        try
        {
            // Wait for the first interval BEFORE running any batch collection.
            // This ensures realtime collectors have time to start and advance their
            // offsets, preventing the batch collector from re-sending events that
            // realtime already sent.
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.BatchIntervalSeconds));
            while (await timer.WaitForNextTickAsync(ct))
            {
                // Persist first to flush any pending in-memory offset updates from
                // realtime collectors to disk, then run batch collectors which will
                // read the latest offsets.
                await _dedupStore.PersistAsync(ct);

                foreach (var batch in _registry.BatchCollectors)
                {
                    try
                    {
                        await batch.CollectBatchAsync(ct);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { }
                }
                await _dedupStore.PersistAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static AgentOptions LoadOptions(string host, int port, string protocol, bool applyFilter)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:SyslogServer:Host"] = host,
                ["Agent:SyslogServer:Port"] = port.ToString(),
                ["Agent:SyslogServer:Protocol"] = protocol,
                ["Agent:Collectors:ApplyFilter"] = applyFilter.ToString()
            })
            .Build();

        var options = new AgentOptions();
        config.GetSection(AgentOptions.SectionName).Bind(options);
        return options;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _loggerFactory.Dispose();

        if (_sender is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
