using SyslogAgent.Syslog;

namespace SyslogAgent.Models;

public enum CollectionMode
{
    Realtime,
    Batch
}

/// <summary>
/// Central DTO flowing through the pipeline from collectors to sender.
/// </summary>
public sealed record LogEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public SyslogFacility Facility { get; init; } = SyslogFacility.Local0;
    public SyslogSeverity Severity { get; init; } = SyslogSeverity.Informational;
    public CollectionMode CollectedBy { get; init; }
    public string DeduplicationKey { get; init; } = string.Empty;
}
