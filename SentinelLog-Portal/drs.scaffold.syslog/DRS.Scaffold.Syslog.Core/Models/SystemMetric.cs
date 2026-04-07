using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>systemmetric</c> table — time-series platform health and throughput metrics.</summary>
[Table("systemmetric")]
public class SystemMetric
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>e.g. cpu_usage, mem_usage, eps_ingest, disk_used_pct</summary>
    [MaxLength(100)]
    [Column("name")]
    public string? Name { get; set; }

    [Column("value")]
    public double? Value { get; set; }

    [Column("metrictime")]
    public DateTimeOffset? MetricTime { get; set; }

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
}
