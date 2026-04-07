using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>appuser</c> table � a platform user account.</summary>
[Table("appuser")]
public class AppUser
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required, MaxLength(50)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt / Argon2 hashed password � never exposed in DTOs.</summary>
    [Required]
    [Column("passwordhash")]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("fullname")]
    public string? FullName { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("lastlogin")]
    public DateTimeOffset? LastLogin { get; set; }

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

    /// <summary>TOTP/MFA enabled flag.</summary>
    [Column("mfaenabled")]
    public bool MfaEnabled { get; set; } = false;

    /// <summary>
    /// Base32-encoded TOTP secret (RFC 6238). Stored encrypted at rest.
    /// Set when user completes MFA enrollment; null when MFA is disabled.
    /// </summary>
    [MaxLength(128)]
    [Column("mfasecret")]
    public string? MfaSecret { get; set; }

    // Navigation
    public ICollection<UserRole>    UserRoles    { get; set; } = new List<UserRole>();
    public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
    public ICollection<ExportJob>   ExportJobs   { get; set; } = new List<ExportJob>();
}
