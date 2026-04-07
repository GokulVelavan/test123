using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>
/// Anomaly detection rule — fires when observed metric deviates from its rolling baseline
/// by more than the configured threshold.
/// </summary>
[Table("anomalyrule")]
public class AnomalyRule
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// What to measure:
    ///   EpsPerSource   – events/second per source IP
    ///   EpsTotal       – total platform EPS
    ///   SeveritySpike  – sudden rise in critical/error events from a host
    ///   NewSourceIp    – first-seen source IP
    ///   AuthFailureRate – auth-failure keyword rate per host
    ///   SilentSource   – source that stops sending (goes quiet)
    /// </summary>
    [MaxLength(50)]
    [Column("metrictype")]
    public string MetricType { get; set; } = "EpsTotal";

    /// <summary>Baseline window in minutes (e.g. 60 = rolling 1-hour average).</summary>
    [Column("baselinewindowminutes")]
    public int BaselineWindowMinutes { get; set; } = 60;

    /// <summary>Multiplier above baseline that triggers the anomaly (e.g. 3.0 = 3× normal).</summary>
    [Column("thresholdmultiplier")]
    public double ThresholdMultiplier { get; set; } = 3.0;

    /// <summary>Absolute minimum value before multiplier applies (avoids noise on near-zero baselines).</summary>
    [Column("absoluteminimum")]
    public double AbsoluteMinimum { get; set; } = 5;

    /// <summary>Critical | High | Medium | Low</summary>
    [MaxLength(20)]
    [Column("severity")]
    public string Severity { get; set; } = "High";

    /// <summary>Auto-create an Incident when this rule fires.</summary>
    [Column("autocreateincident")]
    public bool AutoCreateIncident { get; set; } = false;

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("lastfiredat")]
    public DateTimeOffset? LastFiredAt  { get; set; }

    [Column("firecount")]
    public long            FireCount    { get; set; } = 0;

    [Column("createdat")]
    public DateTimeOffset  CreatedAt    { get; set; } = DateTimeOffset.UtcNow;

    [Column("updatedat")]
    public DateTimeOffset? UpdatedAt    { get; set; }

    [Column("isdeleted")]
    public bool            IsDeleted    { get; set; } = false;
}
