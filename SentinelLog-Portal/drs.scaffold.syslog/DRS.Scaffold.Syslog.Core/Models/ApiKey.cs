using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>apikey</c> table — API access tokens for programmatic access.</summary>
[Table("apikey")]
public class ApiKey
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Column("name")]
    public string? Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>BCrypt hash of the raw key — never stored in plain text.</summary>
    [Column("keyhash")]
    public string? KeyHash { get; set; }

    /// <summary>First 8 characters of the key, safe to display.</summary>
    [MaxLength(16)]
    [Column("keyprefix")]
    public string? KeyPrefix { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("expiresat")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [Column("lastusedat")]
    public DateTimeOffset? LastUsedAt { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(100)]
    [Column("createdby")]
    public string? CreatedBy { get; set; }

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;
}
