using DRS.Scaffold.Syslog.Core.Models;
using System.Text.RegularExpressions;

namespace DRS.Scaffold.Syslog.Worker.Parsing;

/// <summary>
/// Converts a raw <see cref="AuditLog"/> (systemlogs row) into a structured
/// <see cref="LogEvent"/> ready for the logevent table.
/// </summary>
public static partial class SyslogParser
{
    // RFC 5424 PRI field: <nnn>
    [GeneratedRegex(@"^<(?<pri>\d{1,3})>", RegexOptions.Compiled)]
    private static partial Regex PriRegex();

    // Matches Python byte-string literals written into the DB: b'...' or b"..."
    [GeneratedRegex(@"^b['""](?<inner>.*)['""]$", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex ByteLiteralRegex();

    // Windows Event ID patterns: "Event ID: 4624", "EventID 4624", "EventCode: 4624"
    [GeneratedRegex(@"Event\s*ID[:\s]+(?<id>\d+)|EventCode[:\s]+(?<id>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EventCodeRegex();

    private static readonly Dictionary<string, short> KeywordSeverity =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "emergency", 0 }, { "emerg",       0 },
            { "alert",     1 },
            { "critical",  2 }, { "crit",         2 },
            { "error",     3 }, { "err",           3 },
            { "warning",   4 }, { "warn",          4 },
            { "notice",    5 },
            { "info",      6 }, { "information",  6 },
            { "debug",     7 }
        };

    /// <summary>Maps RFC-5424 numeric severity (0-7) to a human-readable label.</summary>
    public static readonly string[] SeverityLabels =
        ["Emergency", "Alert", "Critical", "Error", "Warning", "Notice", "Info", "Debug"];

    // ?????????????????????????????????????????????????????????????????????????

    /// <summary>
    /// Parses <paramref name="raw"/> and returns a new (un-saved) <see cref="LogEvent"/>.
    /// </summary>
    public static LogEvent Parse(AuditLog raw, string serviceAccount)
    {
        // Strip Python b'...' wrappers that arrive from the syslog collector
        var host      = Unwrap(raw.Host);
        var program   = Unwrap(raw.Program);
        var sourceIp  = Unwrap(raw.SourceIp);
        var rawMsg    = Unwrap(raw.Message);

        short? facility = null;
        short? severity = null;
        var    message  = rawMsg ?? string.Empty;

        // Try RFC-5424 PRI field first: <nnn>
        var priMatch = PriRegex().Match(message);
        if (priMatch.Success && int.TryParse(priMatch.Groups["pri"].Value, out var pri))
        {
            facility = (short)(pri >> 3);    // upper 5 bits = facility
            severity = (short)(pri & 0x07);  // lower 3 bits = severity
            message  = message[priMatch.Length..].TrimStart();
        }
        else
        {
            // Fall back to keyword scan on the clean (unwrapped) message
            severity = DetectSeverityFromKeywords(message);
            // Infer facility from program name when no PRI prefix (e.g. Windows events)
            facility = InferFacility(program);
        }

        // Default severity to Info (6) when nothing else matched
        severity ??= 6;

        // Extract Windows Event ID: try pid field first, then scan message text
        string? eventCode = null;
        var pid = raw.Pid?.Trim();
        if (!string.IsNullOrEmpty(pid) && pid != "0" && long.TryParse(pid, out _))
        {
            eventCode = pid;
        }
        else
        {
            var ecMatch = EventCodeRegex().Match(message);
            if (ecMatch.Success)
                eventCode = ecMatch.Groups["id"].Value;
        }

        // Treat systemlogs timestamps as UTC (they are stored without timezone info)
        var eventTime = new DateTimeOffset(
            DateTime.SpecifyKind(raw.LogDatetime, DateTimeKind.Utc));

        return new LogEvent
        {
            EventTime    = eventTime,
            ReceivedTime = DateTimeOffset.UtcNow,
            Hostname     = host,
            SourceIp     = sourceIp,
            Program      = program,
            Facility     = facility,
            Severity     = severity,
            Message      = message,
            RawMessage   = rawMsg,
            EventCode    = eventCode,
            DeviceType   = InferDeviceType(program),
            CreatedAt    = DateTimeOffset.UtcNow,
            CreatedBy    = serviceAccount
        };
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Helpers � public so SyslogPollerWorker can call them during cleanup
    // ?????????????????????????????????????????????????????????????????????????

    /// <summary>
    /// Strips a Python byte-string literal wrapper, e.g. <c>b'foo'</c> ? <c>foo</c>.
    /// Returns the original string unchanged when no wrapper is present.
    /// </summary>
    public static string? Unwrap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var m = ByteLiteralRegex().Match(value.Trim());
        return m.Success ? m.Groups["inner"].Value : value;
    }

    /// <summary>Returns the numeric severity if a severity keyword is found in the message.</summary>
    public static short? DetectSeverityPublic(string message) => DetectSeverityFromKeywords(message);

    /// <summary>Infers a broad device type from the program/process name.</summary>
    public static string? InferDeviceTypePublic(string? program) => InferDeviceType(program);

    private static short? DetectSeverityFromKeywords(string message)
    {
        foreach (var kv in KeywordSeverity)
            if (message.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return null;
    }

    private static short InferFacility(string? program)
    {
        if (string.IsNullOrWhiteSpace(program)) return 1; // user-level
        var p = program.ToLowerInvariant();
        if (p.Contains("kernel") || p.Contains("tpm") || p.Contains("driver"))  return 0; // kernel
        if (p.Contains("security") || p.Contains("audit"))                       return 4; // auth/security
        if (p.Contains("mail") || p.Contains("smtp"))                            return 2; // mail
        if (p.Contains("cron") || p.Contains("task") || p.Contains("scheduler")) return 9; // cron
        if (p.Contains("syslog"))                                                 return 5; // syslogd
        return 1; // user-level default
    }

    private static string? InferDeviceType(string? program)
    {
        if (string.IsNullOrWhiteSpace(program)) return null;
        var p = program.ToLowerInvariant();

        // Security & audit tools
        if (p.Contains("security") || p.Contains("audit")   ||
            p.Contains("apparmor") || p.Contains("selinux") ||
            p.Contains("defender") || p.Contains("sysmon")  ||
            p.Contains("wazuh")    || p.Contains("ossec")   ||
            p.Contains("fail2ban") || p.Contains("applock") ||
            p.Contains("codeintegrity"))                             return "Security";

        // Firewall & network filtering
        if (p.Contains("firewall") || p.Contains("iptables") ||
            p.Contains("nftables") || p.Contains("ufw")      ||
            p.Contains("pf"))                                        return "Firewall";

        // Web servers
        if (p.Contains("nginx")   || p.Contains("apache") ||
            p.Contains("httpd")   || p.Contains("iis")    ||
            p.Contains("tomcat")  || p.Contains("caddy"))           return "WebServer";

        // Databases
        if (p.Contains("mysql")   || p.Contains("postgres") ||
            p.Contains("mssql")   || p.Contains("mongodb")  ||
            p.Contains("redis")   || p.Contains("elastic")  ||
            p.Contains("cassandra"))                                 return "Database";

        // Network services
        if (p.Contains("sshd")    || p.Contains("network") ||
            p.Contains("nmbd")    || p.Contains("smbd")    ||
            p.Contains("openvpn") || p.Contains("vpn")     ||
            p.Contains("named")   || p.Contains("bind")    ||
            p.Contains("dhcp")    || p.Contains("dns"))             return "Network";

        // Containers & orchestration
        if (p.Contains("docker")  || p.Contains("containerd") ||
            p.Contains("kubelet") || p.Contains("podman"))          return "Container";

        // Monitoring & observability
        if (p.Contains("zabbix")  || p.Contains("nagios")  ||
            p.Contains("prometheus") || p.Contains("grafana") ||
            p.Contains("telegraf") || p.Contains("filebeat"))       return "Monitoring";

        // OS & system services
        if (p.Contains("kernel")  || p.Contains("systemd") ||
            p.Contains("init")    || p.Contains("udev")    ||
            p.Contains("dbus")    || p.Contains("cron")    ||
            p.Contains("syslog")  || p.Contains("journald"))        return "OS";

        return "Generic";
    }

    /// <summary>Returns a human-readable label for an RFC-5424 severity code.</summary>
    public static string SeverityLabel(short? severity) =>
        severity is >= 0 and <= 7 ? SeverityLabels[severity.Value] : "Unknown";
}
