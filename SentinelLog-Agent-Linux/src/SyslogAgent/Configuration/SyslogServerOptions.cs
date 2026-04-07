namespace SyslogAgent.Configuration;

/// <summary>
/// Configuration for the remote syslog server.
/// </summary>
public class SyslogServerOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 514;
    public string Protocol { get; set; } = "UDP"; // "UDP" or "TCP"
    public int TcpTimeoutSeconds { get; set; } = 10;
    public int MaxMessageBytes { get; set; } = 1024; // RFC 3164 max
}
