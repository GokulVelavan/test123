using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>notificationchannel</c> table � Email, SNMP, Webhook, Audible targets.</summary>
[Table("notificationchannel")]
public class NotificationChannel
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Column("name")]
    public string? Name { get; set; }

    /// <summary>Email | Slack | Teams | Webhook | PagerDuty | SMS</summary>
    [MaxLength(50)]
    [Column("type")]
    public string? Type { get; set; }

    /// <summary>Email address, webhook URL, SNMP host, etc.</summary>
    [Column("target")]
    public string? Target { get; set; }

    /// <summary>JSON blob � extra config (port, credentials, headers, etc.).</summary>
    [Column("config", TypeName = "jsonb")]
    public string? Config { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

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
    public ICollection<AlertNotification> AlertNotifications { get; set; } = new List<AlertNotification>();
}
