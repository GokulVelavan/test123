using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>role_permission</c> table — many-to-many join between Role and Permission.</summary>
[Table("role_permission")]
public class RolePermission
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("roleid")]
    public long? RoleId { get; set; }

    [Column("permissionid")]
    public long? PermissionId { get; set; }

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
    [ForeignKey(nameof(RoleId))]
    public Role? Role { get; set; }

    [ForeignKey(nameof(PermissionId))]
    public Permission? Permission { get; set; }
}
