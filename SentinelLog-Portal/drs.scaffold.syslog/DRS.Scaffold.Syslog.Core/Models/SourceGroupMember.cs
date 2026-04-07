using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Join table mapping sources to groups (<c>sourcegroupmember</c>).</summary>
[Table("sourcegroupmember")]
public class SourceGroupMember
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("groupid")]
    public long GroupId { get; set; }

    [Column("sourceid")]
    public long SourceId { get; set; }

    // Navigation
    public SourceGroup? Group { get; set; }
    public LogSource?   Source { get; set; }
}
