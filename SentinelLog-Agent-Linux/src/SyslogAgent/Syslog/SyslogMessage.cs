namespace SyslogAgent.Syslog;

/// <summary>
/// Represents a syslog message ready to be formatted and sent.
/// </summary>
public sealed record SyslogMessage
{
    public SyslogFacility Facility { get; init; } = SyslogFacility.Local0;
    public SyslogSeverity Severity { get; init; } = SyslogSeverity.Informational;
    public DateTimeOffset Timestamp { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Calculates PRI field: (Facility * 8) + Severity
    /// </summary>
    public int Priority => ((int)Facility * 8) + (int)Severity;
}
