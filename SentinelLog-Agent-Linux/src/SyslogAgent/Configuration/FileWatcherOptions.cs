using SyslogAgent.Syslog;

namespace SyslogAgent.Configuration;

/// <summary>
/// Configuration for a file watcher collector.
/// </summary>
public class FileWatcherOptions
{
    public string Path { get; set; } = string.Empty;
    public string Filter { get; set; } = "*.log";
    public bool Recursive { get; set; } = false;
    public SyslogFacility Facility { get; set; } = SyslogFacility.Local0;
    public SyslogSeverity Severity { get; set; } = SyslogSeverity.Informational;
}
