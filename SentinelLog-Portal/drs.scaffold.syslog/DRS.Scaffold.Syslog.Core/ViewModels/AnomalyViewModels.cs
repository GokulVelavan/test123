using System.ComponentModel.DataAnnotations;

namespace DRS.Scaffold.Syslog.Core.ViewModels;

public class AnomalyRuleDto
{
    public long    Id                      { get; set; }
    public string  Name                    { get; set; } = string.Empty;
    public string? Description             { get; set; }
    /// <summary>EpsPerSource | EpsTotal | SeveritySpike | NewSourceIp | AuthFailureRate | SilentSource</summary>
    public string  MetricType              { get; set; } = "EpsTotal";
    public int     BaselineWindowMinutes   { get; set; } = 60;
    public double  ThresholdMultiplier     { get; set; } = 3.0;
    public double  AbsoluteMinimum         { get; set; } = 5;
    public string  Severity                { get; set; } = "High";
    public bool    AutoCreateIncident      { get; set; }
    public bool    Enabled                 { get; set; }
    public DateTimeOffset? LastFiredAt     { get; set; }
    public long    FireCount               { get; set; }
    public DateTimeOffset  CreatedAt       { get; set; }
}

public class CreateAnomalyRuleRequest
{
    [Required, MaxLength(200)]
    public string  Name                    { get; set; } = string.Empty;
    public string? Description             { get; set; }
    public string  MetricType              { get; set; } = "EpsTotal";
    public int     BaselineWindowMinutes   { get; set; } = 60;
    public double  ThresholdMultiplier     { get; set; } = 3.0;
    public double  AbsoluteMinimum         { get; set; } = 5;
    public string  Severity                { get; set; } = "High";
    public bool    AutoCreateIncident      { get; set; } = false;
    public bool    Enabled                 { get; set; } = true;
}

public class UpdateAnomalyRuleRequest
{
    [MaxLength(200)]
    public string? Name                    { get; set; }
    public string? Description             { get; set; }
    public string? MetricType              { get; set; }
    public int?    BaselineWindowMinutes   { get; set; }
    public double? ThresholdMultiplier     { get; set; }
    public double? AbsoluteMinimum         { get; set; }
    public string? Severity                { get; set; }
    public bool?   AutoCreateIncident      { get; set; }
    public bool?   Enabled                 { get; set; }
}

public class AnomalyEventDto
{
    public long    Id               { get; set; }
    public long    RuleId           { get; set; }
    public string? RuleName         { get; set; }
    public string? AffectedEntity   { get; set; }
    public double  BaselineValue    { get; set; }
    public double  ObservedValue    { get; set; }
    public double  DeviationRatio   { get; set; }
    public string? Details          { get; set; }
    public bool    Acknowledged     { get; set; }
    public string? AcknowledgedBy   { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset  DetectedAt    { get; set; }
    public string  Severity         { get; set; } = string.Empty;
}

public class AnomalySummaryDto
{
    public int TotalRules      { get; set; }
    public int EnabledRules    { get; set; }
    public int EventsToday     { get; set; }
    public int UnacknowledgedEvents { get; set; }
}
