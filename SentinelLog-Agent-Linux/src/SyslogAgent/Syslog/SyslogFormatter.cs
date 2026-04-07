using System.Text;

namespace SyslogAgent.Syslog;

/// <summary>
/// Formats syslog messages according to RFC 3164.
/// </summary>
public static class SyslogFormatter
{
    private static readonly string[] Months =
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };

    /// <summary>
    /// Formats a syslog message to RFC 3164 byte array.
    /// Format: &lt;PRI&gt;Mmm DD HH:MM:SS HOSTNAME TAG: CONTENT
    ///
    /// Timestamp is always UTC. RFC 3164 carries no timezone field, so UTC is the
    /// industry-standard baseline — all major SIEMs (Splunk, QRadar, Elastic/ECS)
    /// and syslog servers (rsyslog, syslog-ng) expect UTC when no offset is present.
    /// </summary>
    public static byte[] FormatRfc3164(SyslogMessage msg, int maxBytes = 1024)
    {
        var ts = msg.Timestamp.UtcDateTime;
        var month = Months[ts.Month - 1];
        var day = ts.Day < 10 ? $" {ts.Day}" : ts.Day.ToString();
        var time = ts.ToString("HH:mm:ss");

        // Build: <PRI>Mmm DD HH:MM:SS HOSTNAME TAG: CONTENT
        var raw = $"<{msg.Priority}>{month} {day} {time} {msg.Hostname} {msg.Tag}: {msg.Content}";

        var bytes = Encoding.UTF8.GetBytes(raw);

        // Truncate if exceeds maxBytes — must not split multi-byte UTF-8 characters
        if (bytes.Length > maxBytes)
        {
            var truncated = maxBytes;
            // Walk backwards to find a valid UTF-8 boundary
            // A UTF-8 continuation byte starts with 10xxxxxx (0x80-0xBF)
            while (truncated > 0 && (bytes[truncated - 1] & 0xC0) == 0x80)
            {
                truncated--;
            }
            // Now truncated-1 is a leading byte — check if the full character fits
            if (truncated > 0)
            {
                var leadByte = bytes[truncated - 1];
                int charLen;
                if ((leadByte & 0x80) == 0) charLen = 1;
                else if ((leadByte & 0xE0) == 0xC0) charLen = 2;
                else if ((leadByte & 0xF0) == 0xE0) charLen = 3;
                else charLen = 4;

                // If the leading byte's full character doesn't fit in maxBytes, drop it
                if (truncated - 1 + charLen > maxBytes)
                    truncated--;
            }

            Array.Resize(ref bytes, truncated);
        }

        return bytes;
    }
}
