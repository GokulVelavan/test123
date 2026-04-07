using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>logevent</c> table � a single syslog/event entry.</summary>
[Table("logevent")]
public class LogEvent
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("eventtime")]
    public DateTimeOffset EventTime { get; set; }

    [Column("receivedtime")]
    public DateTimeOffset ReceivedTime { get; set; } = DateTimeOffset.UtcNow;

    [Column("sourceid")]
    public long? SourceId { get; set; }

    [Column("hostname")]
    public string? Hostname { get; set; }

    /// <summary>Syslog facility code 0�23 per RFC 5424.</summary>
    [Column("facility")]
    public short? Facility { get; set; }

    /// <summary>Syslog severity 0=Emergency � 7=Debug per RFC 5424.</summary>
    [Column("severity")]
    public short? Severity { get; set; }

    [Column("program")]
    public string? Program { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("rawmessage")]
    public string? RawMessage { get; set; }

    [Column("eventcode")]
    public string? EventCode { get; set; }

    [Column("devicetype")]
    public string? DeviceType { get; set; }

    /// <summary>Transient — populated from the raw systemlogs row during parse, not stored in DB.</summary>
    [NotMapped]
    public string? SourceIp { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(100)]
    [Column("createdby")]
    public string? CreatedBy { get; set; }

    [Column("updatedat")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updatedby")]
    public string? UpdatedBy { get; set; }

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;

    // Navigation
    [ForeignKey(nameof(SourceId))]
    public LogSource? Source { get; set; }

    public ICollection<AlertEvent> AlertEvents { get; set; } = new List<AlertEvent>();
}
