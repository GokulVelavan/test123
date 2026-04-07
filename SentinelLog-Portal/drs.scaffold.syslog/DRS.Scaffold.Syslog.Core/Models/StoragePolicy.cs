using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>storagepolicy</c> table — per-source-type retention and compression settings.</summary>
[Table("storagepolicy")]
public class StoragePolicy
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [MaxLength(50)]
    [Column("sourcetype")]
    public string? SourceType { get; set; }

    [Column("retentiondays")]
    public int? RetentionDays { get; set; }

    [Column("compression")]
    public bool Compression { get; set; } = true;

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
}
