namespace DRS.Scaffold.Syslog.Core.Options;

/// <summary>Bound to the <c>Jwt</c> section in appsettings.json.</summary>
public class JwtSettings
{
    public const string Section = "Jwt";

    /// <summary>HMAC-SHA256 signing key — minimum 32 ASCII characters.</summary>
    public string Key              { get; set; } = string.Empty;
    public string Issuer           { get; set; } = "SentinelLog";
    public string Audience         { get; set; } = "SentinelLogClients";
    /// <summary>Access token lifetime in minutes (default 60).</summary>
    public int    AccessTokenMins  { get; set; } = 60;
    /// <summary>Refresh token lifetime in days (default 7).</summary>
    public int    RefreshTokenDays { get; set; } = 7;
}
