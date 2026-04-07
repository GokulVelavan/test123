using System.Text.Json;
using SyslogAgent.Models;
using SyslogAgent.Syslog;

namespace SyslogAgent.Collectors.Linux;

/// <summary>
/// Shared pure-function helpers for parsing journald JSON output.
/// Used by both JournaldCollector (realtime) and JournaldBatchCollector (batch).
/// </summary>
internal static class JournaldParser
{
    public static LogEvent? Parse(string json, CollectionMode mode)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;

            if (!root.TryGetProperty("MESSAGE", out var messageEl))
                return null;

            var message = messageEl.GetString() ?? string.Empty;

            var timestamp = DateTimeOffset.UtcNow;
            if (root.TryGetProperty("__REALTIME_TIMESTAMP", out var tsEl) &&
                long.TryParse(tsEl.GetString(), out var tsMicros))
            {
                timestamp = new DateTimeOffset(
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMicroseconds(tsMicros));
            }

            var hostname = "localhost";
            if (root.TryGetProperty("_HOSTNAME", out var hostEl))
                hostname = hostEl.GetString() ?? hostname;

            var source = "journal";
            if (root.TryGetProperty("_COMM", out var commEl))
                source = commEl.GetString() ?? source;

            var facility = SyslogFacility.Local0;
            if (root.TryGetProperty("SYSLOG_FACILITY", out var facilEl) &&
                int.TryParse(facilEl.GetString(), out var facil))
                facility = (SyslogFacility)facil;

            var severity = SyslogSeverity.Informational;
            if (root.TryGetProperty("PRIORITY", out var priorEl) &&
                int.TryParse(priorEl.GetString(), out var prior))
                severity = (SyslogSeverity)(prior % 8);

            var cursor = string.Empty;
            if (root.TryGetProperty("__CURSOR", out var cursorEl))
                cursor = cursorEl.GetString() ?? string.Empty;

            return new LogEvent
            {
                Timestamp        = timestamp,
                Hostname         = hostname,
                Source           = source,
                Message          = message,
                Facility         = facility,
                Severity         = severity,
                CollectedBy      = mode,
                DeduplicationKey = cursor
            };
        }
        catch
        {
            return null;
        }
    }

    public static string ExtractCursor(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("__CURSOR", out var el))
                return el.GetString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    public static bool IsValidMatch(string match)
    {
        if (match.Length > 512 || !match.Contains('='))
            return false;
        foreach (var c in match)
        {
            if (!char.IsLetterOrDigit(c) && c != '=' && c != '_' && c != '-' && c != '.' && c != '/' && c != ':')
                return false;
        }
        return true;
    }

    public static bool IsValidCursor(string cursor)
    {
        foreach (var c in cursor)
        {
            if (!char.IsLetterOrDigit(c) && c != '=' && c != ';' && c != ' ' && c != '_' && c != '-')
                return false;
        }
        return cursor.Length > 0 && cursor.Length < 1024;
    }
}
