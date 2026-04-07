using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Fired anomaly detection event — a recorded deviation from baseline.</summary>
[Table("anomalyevent")]
public class AnomalyEvent
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ruleid")]
    public long RuleId { get; set; }
    public AnomalyRule? Rule { get; set; }

    /// <summary>The source/host this anomaly was detected on (null for platform-wide).</summary>
    [MaxLength(255)]
    [Column("affectedentity")]
    public string? AffectedEntity { get; set; }

    [Column("baselinevalue")]
    public double BaselineValue  { get; set; }

    [Column("observedvalue")]
    public double ObservedValue  { get; set; }

    [Column("deviationratio")]
    public double DeviationRatio { get; set; }   // observed / baseline

    [Column("details")]
    public string? Details { get; set; }

    [Column("acknowledged")]
    public bool Acknowledged    { get; set; } = false;

    [Column("acknowledgedby")]
    public string? AcknowledgedBy { get; set; }

    [Column("acknowledgedat")]
    public DateTimeOffset? AcknowledgedAt { get; set; }

    [Column("detectedat")]
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;
}
