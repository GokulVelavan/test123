using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Log forwarding destination — relay received syslog events to an external collector.</summary>
[Table("logforwarder")]
public class LogForwarder
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>UDP | TCP | TLS | HTTP | HTTPS</summary>
    [MaxLength(10)]
    [Column("protocol")]
    public string Protocol { get; set; } = "UDP";

    [MaxLength(255)]
    [Column("host")]
    public string Host { get; set; } = string.Empty;

    [Column("port")]
    public int Port { get; set; } = 514;

    /// <summary>RFC5424 | RFC3164 | JSON | CEF | LEEF</summary>
    [MaxLength(20)]
    [Column("format")]
    public string Format { get; set; } = "RFC5424";

    /// <summary>Filter: only forward events matching this severity (comma-separated). Empty = all.</summary>
    [MaxLength(100)]
    [Column("severityfilter")]
    public string? SeverityFilter { get; set; }

    /// <summary>Filter: only forward events from these source IDs (JSON array). Empty = all.</summary>
    [Column("sourcefilter")]
    public string? SourceFilter { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("tlscertpath")]
    public string? TlsCertPath { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>Running counters.</summary>
    [Column("forwardedcount")]
    public long ForwardedCount { get; set; } = 0;

    [Column("errorcount")]
    public long ErrorCount { get; set; } = 0;

    [Column("lastforwardedat")]
    public DateTimeOffset? LastForwardedAt { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updatedat")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;
}
