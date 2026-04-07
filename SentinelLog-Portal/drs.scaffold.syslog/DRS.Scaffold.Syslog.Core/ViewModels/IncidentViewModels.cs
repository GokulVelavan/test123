using System.ComponentModel.DataAnnotations;

namespace DRS.Scaffold.Syslog.Core.ViewModels;

public class IncidentDto
{
    public long    Id            { get; set; }
    public string  Title         { get; set; } = string.Empty;
    public string? Description   { get; set; }
    /// <summary>Open | Investigating | Contained | Resolved | Closed | FalsePositive</summary>
    public string  Status        { get; set; } = "Open";
    /// <summary>Critical | High | Medium | Low | Informational</summary>
    public string  Priority      { get; set; } = "Medium";
    public long?   AssigneeId    { get; set; }
    public string? AssigneeName  { get; set; }
    public long?   AlertEventId  { get; set; }
    public string? Tags          { get; set; }
    public string? Notes         { get; set; }
    public string? AffectedHosts { get; set; }
    public DateTimeOffset? ResolvedAt  { get; set; }
    public DateTimeOffset  CreatedAt   { get; set; }
    public DateTimeOffset? UpdatedAt   { get; set; }
}

public class CreateIncidentRequest
{
    [Required, MaxLength(200)]
    public string  Title         { get; set; } = string.Empty;
    public string? Description   { get; set; }
    public string  Status        { get; set; } = "Open";
    public string  Priority      { get; set; } = "Medium";
    public long?   AssigneeId    { get; set; }
    public long?   AlertEventId  { get; set; }
    public string? Tags          { get; set; }
    public string? Notes         { get; set; }
    public string? AffectedHosts { get; set; }
}

public class UpdateIncidentRequest
{
    [MaxLength(200)]
    public string? Title         { get; set; }
    public string? Description   { get; set; }
    public string? Status        { get; set; }
    public string? Priority      { get; set; }
    public long?   AssigneeId    { get; set; }
    public string? Tags          { get; set; }
    public string? Notes         { get; set; }
    public string? AffectedHosts { get; set; }
}

public class IncidentSummaryDto
{
    public int TotalOpen        { get; set; }
    public int TotalCritical    { get; set; }
    public int TotalHigh        { get; set; }
    public int TotalResolved    { get; set; }
    public int TotalThisWeek    { get; set; }
}
