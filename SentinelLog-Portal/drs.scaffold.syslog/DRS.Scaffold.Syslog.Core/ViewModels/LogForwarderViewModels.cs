using System.ComponentModel.DataAnnotations;

namespace DRS.Scaffold.Syslog.Core.ViewModels;

public class LogForwarderDto
{
    public long    Id               { get; set; }
    public string  Name             { get; set; } = string.Empty;
    /// <summary>UDP | TCP | TLS | HTTP | HTTPS</summary>
    public string  Protocol         { get; set; } = "UDP";
    public string  Host             { get; set; } = string.Empty;
    public int     Port             { get; set; } = 514;
    /// <summary>RFC5424 | RFC3164 | JSON | CEF | LEEF</summary>
    public string  Format           { get; set; } = "RFC5424";
    public string? SeverityFilter   { get; set; }
    public string? SourceFilter     { get; set; }
    public bool    Enabled          { get; set; }
    public string? Description      { get; set; }
    public string? TlsCertPath { get; set; }
    public long    ForwardedCount   { get; set; }
    public long    ErrorCount       { get; set; }
    public DateTimeOffset? LastForwardedAt { get; set; }
    public DateTimeOffset  CreatedAt       { get; set; }
    public DateTimeOffset? UpdatedAt       { get; set; }
}

public class CreateLogForwarderRequest
{
    [Required, MaxLength(150)]
    public string  Name           { get; set; } = string.Empty;
    public string  Protocol       { get; set; } = "UDP";
    [Required, MaxLength(255)]
    public string  Host           { get; set; } = string.Empty;
    public int     Port           { get; set; } = 514;
    public string  Format         { get; set; } = "RFC5424";
    public string? SeverityFilter { get; set; }
    public string? SourceFilter   { get; set; }
    public bool    Enabled        { get; set; } = true;
    public string? TlsCertPath    { get; set; }
    public string? Description    { get; set; }
}

public class UpdateLogForwarderRequest
{
    [MaxLength(150)]
    public string? Name           { get; set; }
    public string? Protocol       { get; set; }
    [MaxLength(255)]
    public string? Host           { get; set; }
    public int?    Port           { get; set; }
    public string? Format         { get; set; }
    public string? SeverityFilter { get; set; }
    public string? SourceFilter   { get; set; }
    public bool?   Enabled        { get; set; }
    public string? TlsCertPath    { get; set; }
    public string? Description    { get; set; }
}
