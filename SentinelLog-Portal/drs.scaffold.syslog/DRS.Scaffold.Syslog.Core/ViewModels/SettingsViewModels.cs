using System.ComponentModel.DataAnnotations;

namespace DRS.Scaffold.Syslog.Core.ViewModels;

// ?? AppUser ???????????????????????????????????????????????????????????????????

public class UserDto
{
    public long    Id         { get; set; }
    public string  Username   { get; set; } = string.Empty;
    public string  Email      { get; set; } = string.Empty;
    public string? FullName   { get; set; }
    public bool    Active     { get; set; }
    public DateTimeOffset? LastLogin  { get; set; }
    public DateTimeOffset  CreatedAt  { get; set; }
    /// <summary>Role names resolved via user_role → role joins.</summary>
    public List<string> Roles { get; set; } = new();
    /// <summary>#93 — whether TOTP/MFA is enabled for this account.</summary>
    public bool    MfaEnabled { get; set; }
}

// #94 — role assignment
public class AssignRolesRequest
{
    /// <summary>Role names to assign, e.g. ["Admin","Analyst"]. Replaces all existing roles.</summary>
    public List<string> Roles { get; set; } = new();
}

public class CreateUserRequest
{
    [Required, MaxLength(50)]
    public string  Username  { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string  Email     { get; set; } = string.Empty;

    /// <summary>Plain-text password � hashed with BCrypt (work factor 12) before storage.</summary>
    [Required, MinLength(8), MaxLength(128)]
    public string  Password  { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FullName  { get; set; }
}

public class UpdateUserRequest
{
    [MaxLength(200)]
    public string? FullName { get; set; }

    [EmailAddress, MaxLength(255)]
    public string? Email    { get; set; }

    public bool?   Active   { get; set; }
}

// ?? SystemLog (AuditLog) ?????????????????????????????????????????????????????

public class SystemLogDto
{
    public int      Id          { get; set; }
    public DateTime LogDatetime { get; set; }
    public string?  Host        { get; set; }
    public string?  Program     { get; set; }
    public string?  Pid         { get; set; }
    public string?  Message     { get; set; }
    public string?  SourceIp    { get; set; }
}

public class SystemLogFilterRequest
{
    public string?   Host      { get; set; }
    public string?   Program   { get; set; }
    public string?   SourceIp  { get; set; }
    public DateTime? DateFrom  { get; set; }
    public DateTime? DateTo    { get; set; }
    public int       Page      { get; set; } = 1;
    public int       PageSize  { get; set; } = 50;
}

// ?? Auth ??????????????????????????????????????????????????????????????????????

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool    Success      { get; set; }
    public string? Message      { get; set; }
    /// <summary>Signed JWT � include as <c>Authorization: Bearer {Token}</c>.</summary>
    public string? Token        { get; set; }
    /// <summary>Opaque refresh token � stored in <c>usersession</c>.</summary>
    public string? RefreshToken { get; set; }
    /// <summary>UTC expiry of the access token.</summary>
    public DateTimeOffset? TokenExpiresAt { get; set; }
    public UserDto? User        { get; set; }
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

// ?? Platform Settings ?????????????????????????????????????????????????????????

public class PlatformSettingsDto
{
    public string  PlatformName           { get; set; } = string.Empty;
    public string? Organization           { get; set; }
    public string? ContactEmail           { get; set; }
    public string  Timezone               { get; set; } = "UTC";
    public string  DateFormat             { get; set; } = "YYYY-MM-DD HH:mm:ss";
    public bool    ShowMilliseconds        { get; set; } = true;
    public bool    AutoScrollFeed          { get; set; } = true;
    public bool    AudibleAlerts           { get; set; } = true;
    public int     FeedBufferSize          { get; set; } = 5000;
    public int     RefreshIntervalSeconds  { get; set; } = 10;
}

public class UpdatePlatformSettingsRequest
{
    public string? PlatformName           { get; set; }
    public string? Organization           { get; set; }
    public string? ContactEmail           { get; set; }
    public string? Timezone               { get; set; }
    public string? DateFormat             { get; set; }
    public bool?   ShowMilliseconds        { get; set; }
    public bool?   AutoScrollFeed          { get; set; }
    public bool?   AudibleAlerts           { get; set; }
    public int?    FeedBufferSize          { get; set; }
    public int?    RefreshIntervalSeconds  { get; set; }
}

// ?? Storage ???????????????????????????????????????????????????????????????????

public class StorageOverviewDto
{
    public double PrimaryStorageUsedTb   { get; set; }
    public double PrimaryStorageTotalTb  { get; set; }
    public double ArchiveStorageUsedTb   { get; set; }
    public double ArchiveStorageTotalTb  { get; set; }
    public double IngestionRateGbPerHour { get; set; }
    public double CompressionRatio       { get; set; }
}

// ?? StoragePolicy ?????????????????????????????????????????????????????????????

public class StoragePolicyDto
{
    public long    Id            { get; set; }
    public string? SourceType    { get; set; }
    public int?    RetentionDays { get; set; }
    public bool    Compression   { get; set; }
    public DateTimeOffset  CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class UpsertStoragePolicyRequest
{
    public string? SourceType    { get; set; }
    public int?    RetentionDays { get; set; }
    public bool    Compression   { get; set; } = true;
}

// ?? SystemMetric ??????????????????????????????????????????????????????????????

public class SystemMetricDto
{
    public long    Id         { get; set; }
    public string? Name       { get; set; }
    public double? Value      { get; set; }
    public DateTimeOffset? MetricTime { get; set; }
}

// ?? ExportJob ?????????????????????????????????????????????????????????????????

public class ExportJobDto
{
    public long    Id              { get; set; }
    public long?   UserId          { get; set; }
    public string? QueryDefinition { get; set; }
    public string? Format          { get; set; }
    public string? Status          { get; set; }
    public string? FilePath        { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateExportJobRequest
{
    public long?   UserId          { get; set; }
    public string? QueryDefinition { get; set; }
    /// <summary>CSV | JSON | SYSLOG</summary>
    public string  Format          { get; set; } = "CSV";
}

// ── #58/#61 Notification Channels ─────────────────────────────────────────────

public class NotificationChannelDto
{
    public long    Id          { get; set; }
    public string? Name        { get; set; }
    /// <summary>Email | Slack | Teams | Webhook | PagerDuty | SMS</summary>
    public string? Type        { get; set; }
    public string? Target      { get; set; }
    public string? Config      { get; set; }
    public bool    Enabled     { get; set; }
    public DateTimeOffset  CreatedAt  { get; set; }
    public DateTimeOffset? UpdatedAt  { get; set; }
}

public class CreateNotificationChannelRequest
{
    public string? Name    { get; set; }
    public string? Type    { get; set; }
    public string? Target  { get; set; }
    public string? Config  { get; set; }
    public bool    Enabled { get; set; } = true;
}

public class UpdateNotificationChannelRequest
{
    public string? Name    { get; set; }
    public string? Type    { get; set; }
    public string? Target  { get; set; }
    public string? Config  { get; set; }
    public bool?   Enabled { get; set; }
}

// ── #66/#67 Storage History ───────────────────────────────────────────────────

public class StorageHistoryDto
{
    public string Date         { get; set; } = string.Empty;  // "yyyy-MM-dd"
    public int    EventCount   { get; set; }
    public double EstimatedMb  { get; set; }
}

// ── #86-90 Network & Notification Settings ────────────────────────────────────

public class NetworkSettingsDto
{
    public int    UdpPort         { get; set; } = 514;
    public int    TcpPort         { get; set; } = 514;
    public int    TlsPort         { get; set; } = 6514;
    public bool   TlsEnabled      { get; set; } = false;
    public string? TlsCertPath    { get; set; }
    public string? AllowedSubnets { get; set; }
    public int?   MaxConnections  { get; set; }
}

public class NotificationSettingsDto
{
    public string? SmtpHost    { get; set; }
    public int     SmtpPort    { get; set; } = 587;
    public bool    SmtpTls     { get; set; } = true;
    public string? SmtpUser    { get; set; }
    public string? SmtpPass    { get; set; }   // write-only; not pre-filled on load
    public string? FromAddress { get; set; }
}

// ── #95-96 LDAP / Auth Policy ─────────────────────────────────────────────────

public class LdapSettingsDto
{
    public string? Server    { get; set; }
    public int     Port      { get; set; } = 389;
    public bool    UseTls    { get; set; } = false;
    public string? BaseDn    { get; set; }
    public string? BindDn    { get; set; }
    public string? BindPass  { get; set; }   // write-only; not pre-filled on load
    public string? UserAttr  { get; set; }
    public bool    Enabled   { get; set; } = false;
}

public class AuthPolicyDto
{
    public int  SessionTimeoutMinutes  { get; set; } = 480;
    public int  MaxLoginAttempts       { get; set; } = 5;
    public int  LockoutDurationMinutes { get; set; } = 15;
    public bool MfaRequired            { get; set; } = false;
    public int  MinPasswordLength      { get; set; } = 8;
}

// ── #97-98 API Keys ───────────────────────────────────────────────────────────

public class ApiKeyDto
{
    public long    Id          { get; set; }
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? KeyPrefix   { get; set; }
    public bool    Enabled     { get; set; }
    public DateTimeOffset? ExpiresAt    { get; set; }
    public DateTimeOffset? LastUsedAt   { get; set; }
    public DateTimeOffset  CreatedAt    { get; set; }
}

public class CreateApiKeyRequest
{
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class CreateApiKeyResponse : ApiKeyDto
{
    /// <summary>Full key shown ONCE at creation — not stored, only the hash is kept.</summary>
    public string? PlainKey { get; set; }
}

// ── #101-102 License ──────────────────────────────────────────────────────────

public class LicenseDto
{
    public long    Id          { get; set; }
    public string? Licensee    { get; set; }
    public string? Plan        { get; set; }
    public int?    MaxSources  { get; set; }
    public long?   MaxEps      { get; set; }
    public DateTimeOffset? ExpiresAt    { get; set; }
    public DateTimeOffset? ActivatedAt  { get; set; }
    public bool    IsActive    { get; set; }
}

public class ActivateLicenseRequest
{
    public string? LicenseKey { get; set; }
    public string? Licensee   { get; set; }
}

// ── #81 SNMP Trap Forwarding ──────────────────────────────────────────────────

public class SnmpSettingsDto
{
    public bool    Enabled       { get; set; }
    public string? Host          { get; set; }
    public int?    Port          { get; set; }
    public string? Community     { get; set; }
    public string  Version       { get; set; } = "v2c";   // "v1", "v2c", "v3"
    public string? AuthUser      { get; set; }
    public string? AuthPassword  { get; set; }
    public string? PrivPassword  { get; set; }
}

// ── #79 Scheduled Reports ─────────────────────────────────────────────────────

public class ScheduledReportDto
{
    public long    Id          { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Schedule    { get; set; }
    public string  Format      { get; set; } = "csv";
    public bool    Enabled     { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset  CreatedAt { get; set; }
}

public class CreateScheduledReportRequest
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Schedule    { get; set; }
    public string  Format      { get; set; } = "csv";
    public bool    Enabled     { get; set; } = true;
}

// ── #80 Compliance Reports ────────────────────────────────────────────────────

public class ComplianceReportDto
{
    public string    Framework        { get; set; } = string.Empty;
    public string    Period           { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public long      TotalEvents      { get; set; }
    public long      CriticalEvents   { get; set; }
    public long      AuthFailures     { get; set; }
    public long      PolicyViolations { get; set; }
    // Per-framework metric subtitles rendered in the UI
    public string    CriticalLabel    { get; set; } = "Severity 0-2";
    public string    AuthLabel        { get; set; } = "Failed logins";
    public string    PolicyLabel      { get; set; } = "Denied / blocked";
    public List<ComplianceEventDto> RecentCritical { get; set; } = new();
}

public class ComplianceEventDto
{
    public DateTimeOffset Timestamp { get; set; }
    public string? Host     { get; set; }
    public string? Message  { get; set; }
    public string  Severity { get; set; } = string.Empty;
}

// #81 — generic platformconfig access
public class PlatformConfigDto
{
    public string  Category { get; set; } = string.Empty;
    public string  Key      { get; set; } = string.Empty;
    public string? Value    { get; set; }
}

public class SetPlatformConfigRequest
{
    public string  Category { get; set; } = string.Empty;
    public string  Key      { get; set; } = string.Empty;
    public string? Value    { get; set; }
}
