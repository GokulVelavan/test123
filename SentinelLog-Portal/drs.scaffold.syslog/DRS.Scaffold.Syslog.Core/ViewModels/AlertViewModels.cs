namespace DRS.Scaffold.Syslog.Core.ViewModels;

// ?? Alert Rule ????????????????????????????????????????????????????????????????

public class AlertRuleDto
{
    public long    Id                  { get; set; }
    public string? Name                { get; set; }
    public string? Description         { get; set; }
    /// <summary>Critical | Warning | Error | Notice | Info</summary>
    public string? Severity            { get; set; }
    public string? ConditionExpression { get; set; }
    public string? SourceType          { get; set; }
    public int?    TimeWindow          { get; set; }
    public bool    Enabled             { get; set; }
    public DateTimeOffset  CreatedAt   { get; set; }
    public DateTimeOffset? UpdatedAt   { get; set; }
}

public class CreateAlertRuleRequest
{
    public string? Name                { get; set; }
    public string? Description         { get; set; }
    public string? Severity            { get; set; } = "Warning";
    public string? ConditionExpression { get; set; }
    public string? SourceType          { get; set; }
    public int?    TimeWindow          { get; set; }
    public bool    Enabled             { get; set; } = true;
}

public class UpdateAlertRuleRequest
{
    public string? Name                { get; set; }
    public string? Description         { get; set; }
    public string? Severity            { get; set; }
    public string? ConditionExpression { get; set; }
    public string? SourceType          { get; set; }
    public int?    TimeWindow          { get; set; }
    public bool?   Enabled             { get; set; }
}

// ?? Alert Event ???????????????????????????????????????????????????????????????

public class AlertEventDto
{
    public long    Id              { get; set; }
    public long?   RuleId          { get; set; }
    public string? RuleName        { get; set; }
    public long?   LogEventId      { get; set; }
    public string? SourceHost      { get; set; }
    public string? Severity        { get; set; }
    public string? Message         { get; set; }
    public bool    Acknowledged    { get; set; }
    public long?   AcknowledgedBy  { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? TriggeredAt    { get; set; }
    public DateTimeOffset  CreatedAt      { get; set; }
}

public class AlertSummaryDto
{
    public int TotalFiredToday     { get; set; }
    public int CriticalCount       { get; set; }
    public int ErrorCount          { get; set; }
    public int WarningCount        { get; set; }
    public int NoticeCount         { get; set; }
    public int InfoCount           { get; set; }
    public int AcknowledgedCount   { get; set; }
    public int UnacknowledgedCount { get; set; }
}

public class AcknowledgeAlertEventRequest
{
    public long? AcknowledgedBy { get; set; }
}
