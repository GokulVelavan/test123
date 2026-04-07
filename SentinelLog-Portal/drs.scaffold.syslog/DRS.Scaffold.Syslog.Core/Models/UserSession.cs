using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>usersession</c> table — active JWT refresh-token sessions.</summary>
[Table("usersession")]
public class UserSession
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("userid")]
    public long? UserId { get; set; }

    [Column("refreshtoken")]
    public string? RefreshToken { get; set; }

    /// <summary>PostgreSQL <c>inet</c> — stored as string for EF portability.</summary>
    [MaxLength(50)]
    [Column("ipaddress")]
    public string? IpAddress { get; set; }

    [Column("useragent")]
    public string? UserAgent { get; set; }

    [Column("expiresat")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [Column("revoked")]
    public bool Revoked { get; set; } = false;

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

    // Navigation
    [ForeignKey(nameof(UserId))]
    public AppUser? User { get; set; }
}
