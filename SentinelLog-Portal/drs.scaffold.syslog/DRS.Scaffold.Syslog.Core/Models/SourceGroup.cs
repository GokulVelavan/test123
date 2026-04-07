using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>sourcegroup</c> table — a named collection of log sources.</summary>
[Table("sourcegroup")]
public class SourceGroup
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(20)]
    [Column("color")]
    public string? Color { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public ICollection<SourceGroupMember> Members { get; set; } = new List<SourceGroupMember>();
}
