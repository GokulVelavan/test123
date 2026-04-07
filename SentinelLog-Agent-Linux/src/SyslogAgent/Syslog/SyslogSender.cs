using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyslogAgent.Configuration;
using SyslogAgent.Models;

namespace SyslogAgent.Syslog;

public sealed class SyslogSender : ISyslogSender
{
    private readonly SyslogServerOptions _syslogOptions;
    private readonly ILogger<SyslogSender> _logger;
    private UdpClient? _udpClient;
    private TcpClient? _tcpClient;
    private NetworkStream? _tcpStream;

    public SyslogSender(IOptions<AgentOptions> options, ILogger<SyslogSender> logger)
    {
        _syslogOptions = options.Value.SyslogServer;
        _logger = logger;
    }

    public async Task SendAsync(LogEvent evt, CancellationToken cancellationToken)
    {
        try
        {
            var syslogMsg = MapToSyslogMessage(evt);
            var bytes = SyslogFormatter.FormatRfc3164(syslogMsg, _syslogOptions.MaxMessageBytes);

            if (_syslogOptions.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
            {
                await SendTcpWithRetryAsync(bytes, cancellationToken);
            }
            else
            {
                await SendUdpAsync(bytes, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send log event to syslog server");
        }
    }

    /// <summary>
    /// Sends over TCP with one reconnect attempt on failure.
    /// Industry-standard agents retry once on a broken connection before dropping the event.
    /// </summary>
    private async Task SendTcpWithRetryAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            await SendTcpAsync(bytes, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or SocketException or TimeoutException or ObjectDisposedException)
        {
            // Connection was lost — reset state and attempt a single reconnect
            _logger.LogWarning("TCP send failed ({Type}), reconnecting and retrying once", ex.GetType().Name);

            try { _tcpStream?.Dispose(); } catch { /* best effort */ }
            _tcpStream = null;
            _tcpClient?.Dispose();
            _tcpClient = null;

            // Re-throw any failure on the retry so the caller logs it as a real error
            await SendTcpAsync(bytes, cancellationToken);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_tcpStream is not null)
        {
            await _tcpStream.FlushAsync(cancellationToken);
        }
    }

    private async Task SendUdpAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        _udpClient ??= new UdpClient();
        await _udpClient.SendAsync(bytes, _syslogOptions.Host, _syslogOptions.Port, cancellationToken);
    }

    private async Task SendTcpAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        // Ensure TCP connection is established
        if (_tcpClient is null || !_tcpClient.Connected)
        {
            _tcpClient?.Dispose();
            _tcpClient = new TcpClient();

            try
            {
                // Apply configured timeout for TCP connection
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(TimeSpan.FromSeconds(_syslogOptions.TcpTimeoutSeconds));

                await _tcpClient.ConnectAsync(_syslogOptions.Host, _syslogOptions.Port, connectCts.Token);
                _tcpStream = _tcpClient.GetStream();
                _tcpStream.WriteTimeout = _syslogOptions.TcpTimeoutSeconds * 1000;
                _tcpStream.ReadTimeout = _syslogOptions.TcpTimeoutSeconds * 1000;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("TCP connection timed out after {Timeout}s to {Host}:{Port}",
                    _syslogOptions.TcpTimeoutSeconds, _syslogOptions.Host, _syslogOptions.Port);
                _tcpClient?.Dispose();
                _tcpClient = null;
                throw new TimeoutException($"TCP connection to {_syslogOptions.Host}:{_syslogOptions.Port} timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to TCP syslog server at {Host}:{Port}",
                    _syslogOptions.Host, _syslogOptions.Port);
                _tcpClient?.Dispose();
                _tcpClient = null;
                throw;
            }
        }

        // RFC 6587 octet-counting framing: prepend "<length> " before message
        var framePrefix = Encoding.ASCII.GetBytes($"{bytes.Length} ");

        try
        {
            await _tcpStream!.WriteAsync(framePrefix, cancellationToken);
            await _tcpStream.WriteAsync(bytes, cancellationToken);
        }
        catch (IOException ex)
        {
            // Clean up broken connection — caller (SendTcpWithRetryAsync) handles reconnect
            _logger.LogDebug(ex, "TCP write failed, connection state reset");
            try { _tcpStream?.Dispose(); } catch { /* best effort */ }
            _tcpStream = null;
            _tcpClient?.Dispose();
            _tcpClient = null;
            throw;
        }
    }

    private static SyslogMessage MapToSyslogMessage(LogEvent evt)
    {
        return new SyslogMessage
        {
            Facility = evt.Facility,
            Severity = evt.Severity,
            Timestamp = evt.Timestamp,
            Hostname = evt.Hostname,
            Tag = evt.Source.Length > 32 ? evt.Source[..32] : evt.Source,
            Content = evt.Message
        };
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_tcpStream is not null)
        {
            try
            {
                await _tcpStream.FlushAsync();
            }
            catch { /* best effort */ }
            await _tcpStream.DisposeAsync();
            _tcpStream = null;
        }

        _tcpClient?.Dispose();
        _tcpClient = null;

        _udpClient?.Dispose();
        _udpClient = null;
    }
}
