using System.Diagnostics.Eventing.Reader;
using SyslogAgent.Models;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Windows;

/// <summary>
/// Shared pure-function helper for mapping Windows EventRecord to LogEvent.
/// Used by both WindowsEventLogCollector (realtime) and WindowsEventLogBatchCollector (batch).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class WindowsEventLogMapper
{
    public static LogEvent Map(EventRecord record, CollectionMode mode)
    {
        var level    = record.Level ?? 0;
        var severity = level switch
        {
            1 => SyslogSeverity.Critical,
            2 => SyslogSeverity.Error,
            3 => SyslogSeverity.Warning,
            4 => SyslogSeverity.Informational,
            5 => SyslogSeverity.Debug,
            _ => SyslogSeverity.Notice
        };

        var logName  = record.LogName ?? "Unknown";
        var facility = logName switch
        {
            "Security"    => SyslogFacility.Security,
            "System"      => SyslogFacility.System,
            "Application" => SyslogFacility.UserLevel,
            _ when logName.Contains("Security",         StringComparison.OrdinalIgnoreCase)
                || logName.Contains("AppLocker",        StringComparison.OrdinalIgnoreCase)
                || logName.Contains("CodeIntegrity",    StringComparison.OrdinalIgnoreCase)
                || logName.Contains("NTLM",             StringComparison.OrdinalIgnoreCase)
                || logName.Contains("SMB",              StringComparison.OrdinalIgnoreCase)
                || logName.Contains("Defender",         StringComparison.OrdinalIgnoreCase) => SyslogFacility.Security,
            _ when logName.Contains("TerminalServices", StringComparison.OrdinalIgnoreCase)
                || logName.Contains("RemoteDesktop",    StringComparison.OrdinalIgnoreCase) => SyslogFacility.AuthPriv,
            _ when logName.Contains("TaskScheduler",    StringComparison.OrdinalIgnoreCase) => SyslogFacility.Cron,
            _ when logName.Contains("PrintService",     StringComparison.OrdinalIgnoreCase) => SyslogFacility.LinePrinter,
            _ when logName.Contains("DNS",              StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local7,
            _ when logName.Contains("Sysmon",           StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local1,
            _ when logName.Contains("PowerShell",       StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local2,
            _ when logName.Contains("Firewall",         StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local4,
            _ when logName.Contains("WMI",              StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local5,
            _ when logName.Contains("Bits",             StringComparison.OrdinalIgnoreCase) => SyslogFacility.Local6,
            _ when logName.Contains("Driver",           StringComparison.OrdinalIgnoreCase)
                || logName.Contains("Kernel",           StringComparison.OrdinalIgnoreCase) => SyslogFacility.Kernel,
            _ => SyslogFacility.Local0
        };

        string message;
        try   { message = record.FormatDescription() ?? record.ToXml(); }
        catch { message = record.ToXml(); }

        return new LogEvent
        {
            Timestamp        = record.TimeCreated ?? DateTimeOffset.Now,
            Hostname         = Environment.MachineName,
            Source           = record.ProviderName ?? "Unknown",
            Message          = message,
            Facility         = facility,
            Severity         = severity,
            CollectedBy      = mode,
            DeduplicationKey = $"{logName}:{record.RecordId}"
        };
    }
}
