using System.ComponentModel.DataAnnotations;

namespace DRS.Scaffold.Syslog.Core.ViewModels;

public class ThreatIndicatorDto
{
    public long    Id             { get; set; }
    /// <summary>IP | Domain | Hash | URL | Email</summary>
    public string  Type           { get; set; } = "IP";
    public string  Value          { get; set; } = string.Empty;
    public string? ThreatCategory { get; set; }
    public int     Confidence     { get; set; }
    public string? Source         { get; set; }
    public string? Description    { get; set; }
    public string  Origin         { get; set; } = "Manual";
    public bool    IsActive       { get; set; }
    public DateTimeOffset? ExpiresAt  { get; set; }
    public DateTimeOffset  CreatedAt  { get; set; }
}

public class CreateThreatIndicatorRequest
{
    [Required, MaxLength(20)]
    public string  Type           { get; set; } = "IP";
    [Required, MaxLength(512)]
    public string  Value          { get; set; } = string.Empty;
    public string? ThreatCategory { get; set; }
    public int     Confidence     { get; set; } = 80;
    public string? Source         { get; set; }
    public string? Description    { get; set; }
    public bool    IsActive       { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class UpdateThreatIndicatorRequest
{
    public string? ThreatCategory { get; set; }
    public int?    Confidence     { get; set; }
    public string? Source         { get; set; }
    public string? Description    { get; set; }
    public bool?   IsActive       { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class ThreatCheckRequest
{
    [Required]
    public string Value { get; set; } = string.Empty;
}

public class ThreatCheckResultDto
{
    public string  Value       { get; set; } = string.Empty;
    public bool    IsMatched   { get; set; }
    public List<ThreatIndicatorDto> Matches { get; set; } = new();
}

public class BulkImportCsvRequest
{
    /// <summary>Raw CSV content. Header: Type,Value,ThreatCategory,Confidence,Source,Description</summary>
    public string  CsvContent     { get; set; } = string.Empty;
    public string? DefaultSource  { get; set; } = "Bulk Import";
}

public class ThreatIntelSummaryDto
{
    public int TotalIndicators  { get; set; }
    public int ActiveIndicators { get; set; }
    public int IpIndicators     { get; set; }
    public int DomainIndicators { get; set; }
    public int HashIndicators   { get; set; }
    public int CriticalMatches  { get; set; }
}
