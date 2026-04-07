namespace SyslogAgent.Configuration;

/// <summary>
/// Configuration for all log collectors.
/// </summary>
public class CollectorOptions
{
    /// <summary>
    /// When false (default), all logs are collected without filtering.
    /// When true, the configured filters (Event ID whitelist, journald priority/units, syslog patterns) are applied.
    /// </summary>
    public bool ApplyFilter { get; set; } = false;

    public bool WindowsEventLog { get; set; } = true;
    public List<string> WindowsEventLogChannels { get; set; } = new();

    /// <summary>
    /// Per-channel Event ID whitelist. Only events with these IDs are collected.
    /// If a channel is not listed here, ALL events from that channel are collected.
    /// </summary>
    public Dictionary<string, List<int>> WindowsEventLogFilter { get; set; } = new();

    public bool LinuxJournald { get; set; } = true;

    /// <summary>
    /// Journald minimum priority level (0=Emergency to 7=Debug).
    /// Only events at this priority or higher (lower number) are collected.
    /// Industry standard: 4 (Warning) — catches Emergency, Alert, Critical, Error, Warning.
    /// </summary>
    public int LinuxJournaldMaxPriority { get; set; } = 4;

    /// <summary>
    /// Journald unit whitelist. Only events from these systemd units are collected.
    /// Empty list = collect from all units (filtered by priority only).
    /// </summary>
    public List<string> LinuxJournaldUnits { get; set; } = new();

    /// <summary>
    /// Additional journalctl match filters (e.g., "_TRANSPORT=audit", "SYSLOG_FACILITY=4").
    /// </summary>
    public List<string> LinuxJournaldMatches { get; set; } = new();

    public List<string> LinuxSyslogFiles { get; set; } = new();

    /// <summary>
    /// Regex patterns to filter syslog file lines. Only matching lines are forwarded.
    /// Empty list = forward all lines from configured files.
    /// </summary>
    public List<string> LinuxSyslogFilePatterns { get; set; } = new();

    /// <summary>
    /// Enable the dmesg batch collector (kernel ring buffer).
    /// Recommended ONLY on systems without systemd-journald (e.g. Alpine, minimal containers).
    /// When journald is running it already captures kernel messages — enabling both is harmless
    /// but creates duplicate entries.
    /// </summary>
    public bool LinuxDmesg { get; set; } = false;

    /// <summary>
    /// Enable the binary login-record collector:
    ///   wtmp  → successful logins/logouts/reboots  (via `last -F`)
    ///   btmp  → failed login attempts              (via `lastb -F`, requires root)
    ///   lastlog → per-user last-login snapshot     (via `lastlog`)
    /// </summary>
    public bool LinuxBinaryLogs { get; set; } = true;

    /// <summary>
    /// Enable automatic discovery of ALL text log files under /var/log/ and its
    /// subdirectories. Covers application logs not listed in LinuxSyslogFiles
    /// (databases, custom apps, etc.). New files are detected and collected without
    /// an agent restart.
    /// </summary>
    public bool LinuxAutoDiscoverLogs { get; set; } = true;

    public List<FileWatcherOptions> FileWatchers { get; set; } = new();
}
