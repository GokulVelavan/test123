using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>systemlogs</c> table — raw syslog daemon/platform log entries.</summary>
[Table("systemlogs")]
public class AuditLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("log_datetime")]
    public DateTime LogDatetime { get; set; } = DateTime.Now;

    [MaxLength(255)]
    [Column("host")]
    public string? Host { get; set; }

    [MaxLength(255)]
    [Column("program")]
    public string? Program { get; set; }

    [MaxLength(50)]
    [Column("pid")]
    public string? Pid { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [MaxLength(50)]
    [Column("source_ip")]
    public string? SourceIp { get; set; }
}
