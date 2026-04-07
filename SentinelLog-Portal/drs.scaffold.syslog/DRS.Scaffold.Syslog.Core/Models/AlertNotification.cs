using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>alert_notification</c> table — join between AlertRule and NotificationChannel.</summary>
[Table("alert_notification")]
public class AlertNotification
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ruleid")]
    public long? RuleId { get; set; }

    [Column("notificationid")]
    public long? NotificationId { get; set; }

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
    [ForeignKey(nameof(RuleId))]
    public AlertRule? Rule { get; set; }

    [ForeignKey(nameof(NotificationId))]
    public NotificationChannel? NotificationChannel { get; set; }
}
