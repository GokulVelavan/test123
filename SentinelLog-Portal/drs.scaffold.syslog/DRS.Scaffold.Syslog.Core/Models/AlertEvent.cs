using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>alertevent</c> table � a fired instance of an AlertRule.</summary>
[Table("alertevent")]
public class AlertEvent
{
  [Key]
  [Column("id")]
  public long Id { get; set; }

  [Column("ruleid")]
  public long? RuleId { get; set; }

  [Column("logeventid")]
  public long? LogEventId { get; set; }

  [MaxLength(20)]
  [Column("severity")]
  public string? Severity { get; set; }

  [Column("message")]
  public string? Message { get; set; }

  [Column("triggeredat")]
  public DateTimeOffset? TriggeredAt { get; set; }

  [Column("acknowledged")]
  public bool Acknowledged { get; set; } = false;

  [Column("acknowledgedby")]
  public long? AcknowledgedBy { get; set; }

  [Column("acknowledgedat")]
  public DateTimeOffset? AcknowledgedAt { get; set; }

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

  /// <summary>Set by the notification worker after dispatching to all channels.</summary>
  [Column("notifiedat")]
  public DateTimeOffset? NotifiedAt { get; set; }

  [Column("isdeleted")]
  public bool IsDeleted { get; set; } = false;

  // Navigation
  [ForeignKey(nameof(RuleId))]
  public AlertRule? Rule { get; set; }

  [ForeignKey(nameof(LogEventId))]
  public LogEvent? LogEvent { get; set; }
}
