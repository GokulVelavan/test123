using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Threat intelligence indicator (IP, domain, hash, etc.).</summary>
[Table("threatindicator")]
public class ThreatIndicator
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>IP | Domain | Hash | URL | Email</summary>
    [MaxLength(20)]
    [Column("type")]
    public string Type { get; set; } = "IP";

    /// <summary>The indicator value (e.g. "192.168.1.1", "evil.com").</summary>
    [MaxLength(512)]
    [Column("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>Malware | Phishing | BotNet | Scanner | TOR | Proxy | Spam | Ransomware | APT | Other</summary>
    [MaxLength(50)]
    [Column("threatcategory")]
    public string? ThreatCategory { get; set; }

    /// <summary>Confidence score 0–100.</summary>
    [Column("confidence")]
    public int Confidence { get; set; } = 80;

    /// <summary>Source feed name (e.g. "AbuseIPDB", "AlienVault OTX", "Custom").</summary>
    [MaxLength(100)]
    [Column("source")]
    public string? Source { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>Manual | Feed | Auto</summary>
    [MaxLength(20)]
    [Column("origin")]
    public string Origin { get; set; } = "Manual";

    [Column("isactive")]
    public bool IsActive { get; set; } = true;

    [Column("expiresat")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updatedat")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;
}
