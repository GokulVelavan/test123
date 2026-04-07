using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>alertrule</c> table — an operator-defined trigger rule.</summary>
[Table("alertrule")]
public class AlertRule
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(200)]
    [Column("name")]
    public string? Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>Critical | Warning | Error | Notice | Info</summary>
    [MaxLength(20)]
    [Column("severity")]
    public string? Severity { get; set; }

    [Column("conditionexpression")]
    public string? ConditionExpression { get; set; }

    [MaxLength(50)]
    [Column("sourcetype")]
    public string? SourceType { get; set; }

    /// <summary>Evaluation time window in seconds.</summary>
    [Column("timewindow")]
    public int? TimeWindow { get; set; }

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
    public ICollection<AlertEvent>        AlertEvents        { get; set; } = new List<AlertEvent>();
    public ICollection<AlertNotification> AlertNotifications { get; set; } = new List<AlertNotification>();
}
