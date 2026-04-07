using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Managers;

public interface IUserManager
{
    Task<List<UserDto>>  GetUsersAsync();
    Task<UserDto?>       GetUserByIdAsync(long id);
    Task<UserDto>        CreateUserAsync(CreateUserRequest request);
    Task<UserDto?>       UpdateUserAsync(long id, UpdateUserRequest request);
    Task<bool>           DeleteUserAsync(long id);
    /// <summary>#94 — set user roles (replaces existing assignments).</summary>
    Task<UserDto?>       SetRolesAsync(long userId, AssignRolesRequest request);
    /// <summary>Validates credentials, issues JWT + refresh token, writes usersession row.</summary>
    Task<LoginResponse>  LoginAsync(LoginRequest request, string? ipAddress, string? userAgent);
    /// <summary>Validates the refresh token, issues a new access + refresh token pair, rotates the session row.</summary>
    Task<LoginResponse>  RefreshAsync(string refreshToken, string? ipAddress, string? userAgent);
    /// <summary>Revokes the refresh token � logs the session out.</summary>
    Task<bool>           LogoutAsync(string refreshToken);
}

public interface ISystemLogManager
{
    Task<PagedResult<SystemLogDto>> GetSystemLogsAsync(SystemLogFilterRequest filter);
}

public interface ISettingsManager
{
    Task<PlatformSettingsDto>    GetSettingsAsync();
    Task<PlatformSettingsDto>    UpdateSettingsAsync(UpdatePlatformSettingsRequest request);
    Task<StorageOverviewDto>     GetStorageOverviewAsync();
    Task<List<StoragePolicyDto>> GetStoragePoliciesAsync();
    Task<StoragePolicyDto>       UpsertStoragePolicyAsync(UpsertStoragePolicyRequest request);
    Task<bool>                   DeleteStoragePolicyAsync(long id);
    Task<List<SystemMetricDto>>  GetMetricsAsync(string? name, int lastMinutes);
    // #66/#67 — storage history
    Task<List<StorageHistoryDto>> GetStorageHistoryAsync(int days);
    // #86-90 — network + notification settings (platformconfig table)
    Task<NetworkSettingsDto>      GetNetworkSettingsAsync();
    Task<NetworkSettingsDto>      SaveNetworkSettingsAsync(NetworkSettingsDto dto);
    Task<NotificationSettingsDto> GetNotificationSettingsAsync();
    Task<NotificationSettingsDto> SaveNotificationSettingsAsync(NotificationSettingsDto dto);
    // #95-96 — LDAP + auth policy
    Task<LdapSettingsDto>         GetLdapSettingsAsync();
    Task<LdapSettingsDto>         SaveLdapSettingsAsync(LdapSettingsDto dto);
    Task<AuthPolicyDto>           GetAuthPolicyAsync();
    Task<AuthPolicyDto>           SaveAuthPolicyAsync(AuthPolicyDto dto);
    // #81 — SNMP trap forwarding
    Task<SnmpSettingsDto>         GetSnmpSettingsAsync();
    Task<SnmpSettingsDto>         SaveSnmpSettingsAsync(SnmpSettingsDto dto);
    // #79 — Scheduled reports
    Task<List<ScheduledReportDto>> GetScheduledReportsAsync();
    Task<ScheduledReportDto>       CreateScheduledReportAsync(CreateScheduledReportRequest req);
    Task<bool>                     DeleteScheduledReportAsync(long id);
    Task<bool>                     ToggleScheduledReportAsync(long id, bool enabled);
    // #81 — Generic platformconfig access (for parsers, custom categories, etc.)
    Task<List<PlatformConfigDto>>  GetPlatformConfigAsync(string category);
    Task                           SetPlatformConfigAsync(SetPlatformConfigRequest request);
}

// #58/#61 — notification channel manager
public interface INotificationChannelManager
{
    Task<List<NotificationChannelDto>>    GetAllAsync();
    Task<NotificationChannelDto?>         GetByIdAsync(long id);
    Task<NotificationChannelDto>          CreateAsync(CreateNotificationChannelRequest request);
    Task<NotificationChannelDto?>         UpdateAsync(long id, UpdateNotificationChannelRequest request);
    Task<bool>                            DeleteAsync(long id);
    Task<NotificationChannelDto?>         ToggleEnabledAsync(long id, bool enabled);
}

// #97-98 — API key manager
public interface IApiKeyManager
{
    Task<List<ApiKeyDto>>        GetAllAsync();
    Task<CreateApiKeyResponse>   CreateAsync(CreateApiKeyRequest request);
    Task<bool>                   DeleteAsync(long id);
    Task<ApiKeyDto?>             ToggleEnabledAsync(long id, bool enabled);
}

// #101-102 — license manager
public interface ILicenseManager
{
    Task<LicenseDto?>            GetActiveLicenseAsync();
    Task<LicenseDto>             ActivateAsync(ActivateLicenseRequest request);
    Task<bool>                   DeactivateAsync(long id);
}

// #79/#80 — reports manager
public interface IReportsManager
{
    Task<List<ScheduledReportDto>> GetScheduledReportsAsync();
    Task<ScheduledReportDto>       CreateAsync(CreateScheduledReportRequest req);
    Task<bool>                     DeleteAsync(long id);
    Task<bool>                     ToggleAsync(long id, bool enabled);
    Task<ComplianceReportDto>      GenerateComplianceReportAsync(string framework, int days);
    Task<(byte[] Content, string ContentType, string FileName)?> RunReportAsync(long id);
}
