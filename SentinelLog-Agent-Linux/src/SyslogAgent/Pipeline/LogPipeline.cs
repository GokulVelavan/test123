using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyslogAgent.Configuration;
using SyslogAgent.Syslog;

namespace SyslogAgent.Pipeline;

public sealed class LogPipeline : BackgroundService
{
    private readonly LogChannel _channel;
    private readonly ISyslogSender _sender;
    private readonly ILogger<LogPipeline> _logger;
    private readonly SyslogServerOptions _serverOpts;

    public LogPipeline(
        LogChannel channel,
        ISyslogSender sender,
        IOptions<AgentOptions> options,
        ILogger<LogPipeline> logger)
    {
        _channel    = channel;
        _sender     = sender;
        _serverOpts = options.Value.SyslogServer;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogPipeline started, waiting for log events");

        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _sender.SendAsync(evt, stoppingToken);

                    var msg = evt.Message.Length > 120 ? evt.Message[..117] + "..." : evt.Message;
                    msg = msg.ReplaceLineEndings(" ");

                    ConsoleWriter.WriteLineSegments(
                        ($"  [{evt.Timestamp:HH:mm:ss}] ", ConsoleColor.DarkGray),
                        (evt.Source,                       ConsoleColor.White),
                        (" → ",                            ConsoleColor.DarkGray),
                        ($"{_serverOpts.Protocol}://{_serverOpts.Host}:{_serverOpts.Port}", ConsoleColor.Green),
                        ($" | {evt.Severity} | ",          ConsoleColor.DarkGray),
                        (msg,                              null));
                }
                catch (Exception ex)
                {
                    ConsoleWriter.WriteLine($"  [ERROR] Failed to send: {ex.Message}", ConsoleColor.Red);
                    _logger.LogError(ex, "Failed to send log event");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("LogPipeline stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LogPipeline encountered an unexpected error");
            throw;
        }

        while (_channel.Reader.TryRead(out var remaining))
        {
            try
            {
                await _sender.SendAsync(remaining, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send remaining event during shutdown");
                break;
            }
        }

        await _sender.FlushAsync(CancellationToken.None);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
