namespace DRS.Scaffold.Syslog.WebUI.Models;

// ── Auth ─────────────────────────────────────────────────────────────────────

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool    Success        { get; set; }
    public string? Message        { get; set; }
    public string? Token          { get; set; }
    public string? RefreshToken   { get; set; }
    public DateTimeOffset? TokenExpiresAt { get; set; }
    public UserDto? User          { get; set; }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class VerifyMfaRequest
{
    public string  Code     { get; set; } = string.Empty;
    public string? Username { get; set; }
}

// ── User ─────────────────────────────────────────────────────────────────────

public class UserDto
{
    public long    Id         { get; set; }
    public string  Username   { get; set; } = string.Empty;
    public string  Email      { get; set; } = string.Empty;
    public string? FullName   { get; set; }
    public bool    Active     { get; set; }
    public DateTimeOffset? LastLogin  { get; set; }
    public DateTimeOffset  CreatedAt  { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool    MfaEnabled { get; set; }
}

public class AssignRolesRequest
{
    public List<string> Roles { get; set; } = new();
}

public class CreateUserRequest
{
    public string  Username  { get; set; } = string.Empty;
    public string  Email     { get; set; } = string.Empty;
    /// <summary>Plain-text — hashed with BCrypt before storage.</summary>
    public string  Password  { get; set; } = string.Empty;
    public string? FullName  { get; set; }
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Email    { get; set; }
    public bool?   Active   { get; set; }
}

// ── Logs ─────────────────────────────────────────────────────────────────────

public class LogEventDto
{
    public long    Id            { get; set; }
    public long?   SourceId      { get; set; }
    public string? Hostname      { get; set; }
    public short?  Facility      { get; set; }
    public short?  Severity      { get; set; }
    public string? Program       { get; set; }
    public string? Message       { get; set; }
    public string? RawMessage    { get; set; }
    public string? EventCode     { get; set; }
    public string? DeviceType    { get; set; }
    public string? SourceIp      { get; set; }
    public DateTimeOffset  EventTime    { get; set; }
    public DateTimeOffset  ReceivedTime { get; set; }
    public string? SeverityLabel { get; set; }
    public string? FacilityLabel { get; set; }
    // Legacy aliases kept for backward compat with existing views
    public string? Host       => Hostname;
    public DateTime LogDatetime => EventTime.UtcDateTime;
}

public class DashboardSummaryDto
{
    public long TotalEventsToday { get; set; }
    public int  CriticalAlerts   { get; set; }
    public int  ErrorsPerHour    { get; set; }
    public int  Warnings         { get; set; }
    public int     ActiveSources  { get; set; }
    public decimal CurrentEps    { get; set; }
}

public class LiveLogQueryRequest
{
    public int     LastSeconds { get; set; } = 60;
    public short?  Severity    { get; set; }
    public string? DeviceType  { get; set; }
    public int     Limit       { get; set; } = 200;
}

public class LogSearchRequest
{
    public string?         Keyword    { get; set; }
    public string?         Hostname   { get; set; }
    public short?          Severity   { get; set; }
    public short?          Facility   { get; set; }
    public string?         DeviceType { get; set; }
    public string?         Program    { get; set; }
    public DateTimeOffset? TimeFrom   { get; set; }
    public DateTimeOffset? TimeTo     { get; set; }
    public int             Page       { get; set; } = 1;
    public int             PageSize   { get; set; } = 50;
    public bool            Descending { get; set; } = true;
}

public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

/// <summary>Bucketed event volume — each bucket = one time slice.</summary>
public class EventVolumeDto
{
    public DateTimeOffset BucketTime { get; set; }
    public int Critical { get; set; }
    public int Error    { get; set; }
    public int Warning  { get; set; }
    public int Info     { get; set; }
    public int Debug    { get; set; }
    public int Total    { get; set; }
}

public class SeverityBreakdownDto
{
    public long Critical { get; set; }
    public long Error    { get; set; }
    public long Warning  { get; set; }
    public long Notice   { get; set; }
    public long Info     { get; set; }
    public long Debug    { get; set; }
    public long Total    { get; set; }
}

public class TopTalkerDto
{
    public int     Rank       { get; set; }
    public string  Host       { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public long    EventCount { get; set; }
    /// <summary>Volume in GB — may be populated by manager enrichment.</summary>
    public double  VolumeGb   { get; set; }
}

// ── Alerts ───────────────────────────────────────────────────────────────────

public class AlertRuleDto
{
    public long    Id                  { get; set; }
    public string? Name                { get; set; }
    public string? Description         { get; set; }
    public string? Severity            { get; set; }
    public string? ConditionExpression { get; set; }
    public string? SourceType          { get; set; }
    public int?    TimeWindow          { get; set; }
    public bool    Enabled             { get; set; }
    public DateTimeOffset  CreatedAt   { get; set; }
    public DateTimeOffset? UpdatedAt   { get; set; }
}

public class CreateAlertRuleRequest
{
    public string? Name                { get; set; }
    public string? Description         { get; set; }
    public string? Severity            { get; set; } = "Warning";
    public string? ConditionExpression { get; set; }
    public string? SourceType          { get; set; }
    public int?    TimeWindow          { get; set; }
    public bool    Enabled             { get; set; } = true;
}

public class UpdateAlertRuleRequest
{
    public string? Name                { get; set; }
    public string? Description         { get; set; }
    public string? Severity            { get; set; }
    public string? ConditionExpression { get; set; }
    public string? SourceType          { get; set; }
    public int?    TimeWindow          { get; set; }
    public bool?   Enabled             { get; set; }
}

public class AlertEventDto
{
    public long    Id              { get; set; }
    public long?   RuleId          { get; set; }
    public string? RuleName        { get; set; }
    public long?   LogEventId      { get; set; }
    public string? SourceHost      { get; set; }
    public string? Severity        { get; set; }
    public string? Message         { get; set; }
    public bool    Acknowledged    { get; set; }
    public long?   AcknowledgedBy  { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? TriggeredAt    { get; set; }
    public DateTimeOffset  CreatedAt      { get; set; }
    // Convenience alias so existing JS (e.triggeredAt / e.firedAt) works
    public DateTimeOffset? FiredAt => TriggeredAt;
}

public class AcknowledgeAlertEventRequest
{
    public long? AcknowledgedBy { get; set; }
}

public class AlertSummaryDto
{
    public int TotalFiredToday     { get; set; }
    public int CriticalCount       { get; set; }
    public int WarningCount        { get; set; }
    public int InfoCount           { get; set; }
    public int AcknowledgedCount   { get; set; }
    public int UnacknowledgedCount { get; set; }
}

// ── Sources ──────────────────────────────────────────────────────────────────

public class LogSourceDto
{
    public long    Id         { get; set; }
    public string? Name       { get; set; }
    public string? IpAddress  { get; set; }
    public string? DeviceType { get; set; }
    public string? OsType     { get; set; }
    public string? Location   { get; set; }
    public string? Status     { get; set; }
    public DateTimeOffset? LastSeen  { get; set; }
    public DateTimeOffset  CreatedAt { get; set; }
    public string? CreatedBy  { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy  { get; set; }
    public int     CurrentEps { get; set; }
}

// #72 — bulk requests
public class BulkSourceIdsRequest
{
    public List<long> Ids { get; set; } = new();
}

public class BulkSourceStatusRequest
{
    public List<long> Ids    { get; set; } = new();
    public string     Status { get; set; } = "Offline";
}

// #18/#22/#23 — activity
public class SourceActivityDto
{
    public DateTimeOffset BucketTime { get; set; }
    public int            EventCount { get; set; }
}

public class LogSourceSummaryDto
{
    public int TotalSources  { get; set; }
    public int OnlineCount   { get; set; }
    public int OfflineCount  { get; set; }
    public int DegradedCount { get; set; }
    public int PendingCount  { get; set; }
}

public class CreateLogSourceRequest
{
    public string? Name       { get; set; }
    public string? IpAddress  { get; set; }
    public string? DeviceType { get; set; }
    public string? OsType     { get; set; }
    public string? Location   { get; set; }
    public string  Status     { get; set; } = "Pending";
    public string? CreatedBy  { get; set; }
}

public class UpdateLogSourceRequest
{
    public string? Name       { get; set; }
    public string? IpAddress  { get; set; }
    public string? DeviceType { get; set; }
    public string? OsType     { get; set; }
    public string? Location   { get; set; }
    public string? Status     { get; set; }
    public string? UpdatedBy  { get; set; }
}

// ── Settings ─────────────────────────────────────────────────────────────────

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

public class StorageOverviewDto
{
    public double PrimaryStorageUsedTb    { get; set; }
    public double PrimaryStorageTotalTb   { get; set; }
    public double ArchiveStorageUsedTb    { get; set; }
    public double ArchiveStorageTotalTb   { get; set; }
    public double IngestionRateGbPerHour  { get; set; }
    public double CompressionRatio        { get; set; }
}

public class StoragePolicyDto
{
    public long    Id             { get; set; }
    public string? SourceType     { get; set; }
    public int?    RetentionDays  { get; set; }
    public bool    Compression    { get; set; }
    public DateTimeOffset  CreatedAt  { get; set; }
    public DateTimeOffset? UpdatedAt  { get; set; }
}

public class UpsertStoragePolicyRequest
{
    public string? SourceType    { get; set; }
    public int?    RetentionDays { get; set; }
    public bool    Compression   { get; set; }
}

public class SystemMetricDto
{
    public long    Id         { get; set; }
    public string? Name       { get; set; }
    public double? Value      { get; set; }
    public DateTimeOffset? MetricTime { get; set; }
}

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
    public string?   Host     { get; set; }
    public string?   Program  { get; set; }
    public string?   SourceIp { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo   { get; set; }
    public int       Page     { get; set; } = 1;
    public int       PageSize { get; set; } = 50;
}

// ── #41 Facets ────────────────────────────────────────────────────────────────

public class LogFacetDto
{
    public string          Field { get; set; } = string.Empty;
    public List<FacetItem> Items { get; set; } = new();
}

public class FacetItem
{
    public string Value { get; set; } = string.Empty;
    public long   Count { get; set; }
}

// ── #51 Heatmap ───────────────────────────────────────────────────────────────

public class HeatmapBucketDto
{
    public int Day        { get; set; }
    public int Hour       { get; set; }
    public int EventCount { get; set; }
}

// ── #52 Category Breakdown ────────────────────────────────────────────────────

public class CategoryBreakdownDto
{
    public string Category   { get; set; } = string.Empty;
    public long   EventCount { get; set; }
}

// ── #58/#61 Notification Channels ────────────────────────────────────────────

public class NotificationChannelDto
{
    public long    Id        { get; set; }
    public string? Type      { get; set; }
    public string? Target    { get; set; }
    public string? Config    { get; set; }
    public bool    Enabled   { get; set; }
    public DateTimeOffset  CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class CreateNotificationChannelRequest
{
    public string? Type    { get; set; }
    public string? Target  { get; set; }
    public string? Config  { get; set; }
    public bool    Enabled { get; set; } = true;
}

public class UpdateNotificationChannelRequest
{
    public string? Type    { get; set; }
    public string? Target  { get; set; }
    public string? Config  { get; set; }
    public bool?   Enabled { get; set; }
}

// ── #66/#67 Storage History ───────────────────────────────────────────────────

public class StorageHistoryDto
{
    public DateTimeOffset Timestamp       { get; set; }
    public double         PrimaryUsedTb   { get; set; }
    public double         ArchiveUsedTb   { get; set; }
}

// ── #86-90 Network & Notification Settings ────────────────────────────────────

public class NetworkSettingsDto
{
    public int    SyslogUdpPort   { get; set; } = 514;
    public int    SyslogTcpPort   { get; set; } = 514;
    public bool   TlsEnabled      { get; set; } = false;
    public string? TlsCertPath    { get; set; }
    public string? AllowedSubnets { get; set; }
}

public class NotificationSettingsDto
{
    public string? SmtpHost     { get; set; }
    public int     SmtpPort     { get; set; } = 587;
    public bool    SmtpTls      { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? FromAddress  { get; set; }
}

// ── #95-96 LDAP / Auth Policy ─────────────────────────────────────────────────

public class LdapSettingsDto
{
    public string? Host    { get; set; }
    public int     Port    { get; set; } = 389;
    public bool    UseSsl  { get; set; } = false;
    public string? BaseDn  { get; set; }
    public string? BindDn  { get; set; }
    public bool    Enabled { get; set; } = false;
}

public class AuthPolicyDto
{
    public int  SessionTimeoutMinutes    { get; set; } = 480;
    public int  MaxFailedLoginAttempts   { get; set; } = 5;
    public int  LockoutDurationMinutes   { get; set; } = 15;
    public bool RequireMfa               { get; set; } = false;
    public int  PasswordMinLength        { get; set; } = 8;
}

// ── #97-98 API Keys ───────────────────────────────────────────────────────────

public class ApiKeyDto
{
    public long    Id          { get; set; }
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? KeyPrefix   { get; set; }
    public bool    Enabled     { get; set; }
    public DateTimeOffset? ExpiresAt  { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset  CreatedAt  { get; set; }
}

public class CreateApiKeyRequest
{
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class CreateApiKeyResponse : ApiKeyDto
{
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
    public DateTimeOffset? ExpiresAt   { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public bool    IsActive    { get; set; }
}

public class ActivateLicenseRequest
{
    public string? LicenseKey { get; set; }
}
