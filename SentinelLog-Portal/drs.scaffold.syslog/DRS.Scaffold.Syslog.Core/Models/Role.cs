using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>role</c> table — e.g. Admin, Analyst, ReadOnly, DevOps.</summary>
[Table("role")]
public class Role
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required, MaxLength(50)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

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
    public ICollection<UserRole>       UserRoles       { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
