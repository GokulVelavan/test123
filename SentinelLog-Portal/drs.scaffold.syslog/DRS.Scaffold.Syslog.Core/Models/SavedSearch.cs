using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>User-saved search queries for quick recall.</summary>
[Table("savedsearch")]
public class SavedSearch
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>JSON-serialised LogSearchRequest filters.</summary>
    [Column("queryjson")]
    public string QueryJson { get; set; } = "{}";

    [Column("userid")]
    public long? UserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary>Whether visible to all users (true) or owner-only (false).</summary>
    [Column("isshared")]
    public bool IsShared { get; set; } = false;

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updatedat")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;
}
