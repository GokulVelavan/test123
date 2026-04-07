using SyslogAgent.Models;

namespace SyslogAgent.Syslog;

/// <summary>
/// Sends log events as syslog messages to a remote server.
/// </summary>
public interface ISyslogSender : IAsyncDisposable
{
    /// <summary>
    /// Sends a log event as a syslog message.
    /// </summary>
    Task SendAsync(LogEvent logEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Flushes any pending data (e.g., TCP stream buffer).
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken);
}
