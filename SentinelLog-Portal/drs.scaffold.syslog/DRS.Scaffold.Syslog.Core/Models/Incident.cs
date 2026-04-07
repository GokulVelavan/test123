using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Security incident / case created from one or more alert events.</summary>
[Table("incident")]
public class Incident
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>Open | Investigating | Contained | Resolved | Closed | FalsePositive</summary>
    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = "Open";

    /// <summary>Critical | High | Medium | Low | Informational</summary>
    [MaxLength(20)]
    [Column("priority")]
    public string Priority { get; set; } = "Medium";

    /// <summary>FK to AppUser who is assigned to investigate.</summary>
    [Column("assigneeid")]
    public long? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }

    /// <summary>FK to the AlertEvent that triggered this incident (optional).</summary>
    [Column("alerteventid")]
    public long? AlertEventId { get; set; }
    public AlertEvent? AlertEvent { get; set; }

    [Column("tags")]
    public string? Tags { get; set; }       // comma-separated

    [Column("notes")]
    public string? Notes { get; set; }      // analyst notes / timeline markdown

    [Column("affectedhosts")]
    public string? AffectedHosts { get; set; }  // JSON array of hostnames

    [Column("resolvedat")]
    public DateTimeOffset? ResolvedAt { get; set; }

    [Column("resolvedbyid")]
    public long? ResolvedById { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updatedat")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [Column("createdbyid")]
    public long? CreatedById { get; set; }

    [Column("isdeleted")]
    public bool IsDeleted { get; set; } = false;
}
