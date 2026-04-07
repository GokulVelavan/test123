using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DRS.Scaffold.Syslog.Core.Models;

/// <summary>Maps to the <c>source</c> table � a log-emitting device registered in the platform.</summary>
[Table("source")]
public class LogSource
{
  [Key]
  [Column("id")]
  public long Id { get; set; }

  [MaxLength(200)]
  [Column("name")]
  public string? Name { get; set; }

  /// <summary>PostgreSQL <c>inet</c> type � mapped to string for EF portability.</summary>
  [MaxLength(50)]
  [Column("ipaddress")]
  public string? IpAddress { get; set; }

  [MaxLength(50)]
  [Column("devicetype")]
  public string? DeviceType { get; set; }

  [MaxLength(50)]
  [Column("ostype")]
  public string? OsType { get; set; }

  [MaxLength(200)]
  [Column("location")]
  public string? Location { get; set; }

  /// <summary>Online | Offline | Degraded | Pending</summary>
  [MaxLength(20)]
  [Column("status")]
  public string? Status { get; set; }

  [Column("lastseen")]
  public DateTimeOffset? LastSeen { get; set; }

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

  // ── GeoIP enrichment ───────────────────────────────────────────────────────
  [MaxLength(2)]
  [Column("geocountrycode")]
  public string? GeoCountryCode { get; set; }   // e.g. "US"

  [MaxLength(100)]
  [Column("geocountry")]
  public string? GeoCountry { get; set; }        // e.g. "United States"

  [MaxLength(100)]
  [Column("geocity")]
  public string? GeoCity { get; set; }

  [Column("geolat")]
  public double? GeoLat { get; set; }

  [Column("geolon")]
  public double? GeoLon { get; set; }

  [MaxLength(60)]
  [Column("geoasn")]
  public string? GeoAsn { get; set; }           // Autonomous System Number, e.g. "AS15169 Google LLC"

  // Navigation
  public ICollection<LogEvent> LogEvents { get; set; } = new List<LogEvent>();
}
