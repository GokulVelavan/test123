using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

[Table("scheduledreport")]
public class ScheduledReport
{
    [Column("id")]          public long    Id          { get; set; }
    [Column("name")]        public string  Name        { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("schedule")]    public string? Schedule    { get; set; }
    [Column("filtersjson")] public string? FiltersJson { get; set; }
    [Column("format")]      public string  Format      { get; set; } = "csv";
    [Column("enabled")]     public bool    Enabled     { get; set; } = true;
    [Column("lastrunathz")] public DateTimeOffset? LastRunAt { get; set; }
    [Column("createdat")]   public DateTimeOffset  CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Column("isdeleted")]   public bool    IsDeleted   { get; set; }
}
