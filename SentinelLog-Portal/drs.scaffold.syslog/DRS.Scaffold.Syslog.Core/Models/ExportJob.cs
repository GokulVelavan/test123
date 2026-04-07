using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>exportjob</c> table — a background log-export task requested by a user.</summary>
[Table("exportjob")]
public class ExportJob
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("userid")]
    public long? UserId { get; set; }

    /// <summary>JSON blob — the log search/filter query that produced this export.</summary>
    [Column("querydefinition", TypeName = "jsonb")]
    public string? QueryDefinition { get; set; }

    /// <summary>CSV | JSON | SYSLOG</summary>
    [MaxLength(20)]
    [Column("format")]
    public string? Format { get; set; }

    /// <summary>Pending | Running | Completed | Failed</summary>
    [MaxLength(20)]
    [Column("status")]
    public string? Status { get; set; }

    [Column("filepath")]
    public string? FilePath { get; set; }

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
