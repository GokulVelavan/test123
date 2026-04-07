using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>platformconfig</c> table — key/value config store for network, LDAP, auth, etc.</summary>
[Table("platformconfig")]
public class PlatformConfig
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>Config namespace: network | ldap | auth | notifications | snmp</summary>
    [MaxLength(50)]
    [Column("category")]
    public string? Category { get; set; }

    [MaxLength(100)]
    [Column("key")]
    public string? Key { get; set; }

    [Column("value")]
    public string? Value { get; set; }

    [Column("createdat")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updatedat")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
