using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>license</c> table — platform license key records.</summary>
[Table("license")]
public class License
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>Opaque license key string submitted by the user.</summary>
    [Column("licensekey")]
    public string? LicenseKey { get; set; }

    [MaxLength(200)]
    [Column("licensee")]
    public string? Licensee { get; set; }

    /// <summary>Community | Professional | Enterprise</summary>
    [MaxLength(50)]
    [Column("plan")]
    public string? Plan { get; set; }

    [Column("maxsources")]
    public int? MaxSources { get; set; }

    [Column("maxeps")]
    public long? MaxEps { get; set; }

    [Column("expiresat")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [Column("isactive")]
    public bool IsActive { get; set; } = true;

    [Column("activatedat")]
    public DateTimeOffset? ActivatedAt { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;
}
