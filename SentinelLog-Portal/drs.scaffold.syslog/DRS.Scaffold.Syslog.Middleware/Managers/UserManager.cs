using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using DRS.Scaffold.Syslog.Middleware.Repositories;
using DRS.Scaffold.Syslog.Middleware.Services;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

// ?? UserManager ???????????????????????????????????????????????????????????????
public class UserManager : IUserManager
{
    private readonly IUserRepository    _users;
    private readonly ISessionRepository _sessions;
    private readonly JwtService         _jwt;

    public UserManager(
        IUserRepository    users,
        ISessionRepository sessions,
        JwtService         jwt)
    {
        _users    = users;
        _sessions = sessions;
        _jwt      = jwt;
    }

    public Task<List<UserDto>> GetUsersAsync()                                  => _users.GetAllAsync();
    public Task<UserDto?>      GetUserByIdAsync(long id)                        => _users.GetByIdAsync(id);
    public Task<UserDto>       CreateUserAsync(CreateUserRequest req)           => _users.CreateAsync(req);
    public Task<UserDto?>      UpdateUserAsync(long id, UpdateUserRequest req)  => _users.UpdateAsync(id, req);
    public Task<bool>          DeleteUserAsync(long id)                         => _users.DeleteAsync(id);
    public Task<UserDto?>      SetRolesAsync(long userId, AssignRolesRequest req) => _users.SetRolesAsync(userId, req.Roles);

    // ?? Login ?????????????????????????????????????????????????????????????????
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request, string? ipAddress, string? userAgent)
    {
        // 1. Load raw entity (has PasswordHash)
        var entity = await _users.GetEntityByUsernameAsync(request.Username);

        // 2. Constant-time failure � same message for "not found" and "wrong password"
        if (entity is null || !entity.Active)
            return Fail("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, entity.PasswordHash))
            return Fail("Invalid username or password.");

        // 3. Stamp last-login
        await _users.UpdateLastLoginAsync(entity.Id);

        // 4. Build safe DTO (no hash)
        var userDto = await _users.GetByIdAsync(entity.Id)
                      ?? throw new InvalidOperationException("User vanished after login.");

        // 5. Issue tokens
        var (accessToken,  accessExpiry)  = _jwt.GenerateAccessToken(userDto);
        var (refreshToken, refreshExpiry) = _jwt.GenerateRefreshToken();

        // 6. Persist session row in usersession
        await _sessions.CreateAsync(
            entity.Id, refreshToken, refreshExpiry, ipAddress, userAgent);

        return new LoginResponse
        {
            Success        = true,
            Token          = accessToken,
            RefreshToken   = refreshToken,
            TokenExpiresAt = accessExpiry,
            User           = userDto
        };
    }

    // ?? Refresh ???????????????????????????????????????????????????????????????
    public async Task<LoginResponse> RefreshAsync(
        string refreshToken, string? ipAddress, string? userAgent)
    {
        // 1. Find active, non-expired session
        var session = await _sessions.GetActiveByTokenAsync(refreshToken);
        if (session?.User is null || !session.UserId.HasValue)
            return Fail("Invalid or expired refresh token.");

        var userId  = session.UserId.Value;
        var userDto = await _users.GetByIdAsync(userId);
        if (userDto is null || !userDto.Active)
            return Fail("User account is inactive.");

        // 2. Revoke the used refresh token (token rotation � each token is single-use)
        await _sessions.RevokeAsync(refreshToken);

        // 3. Issue a new pair
        var (accessToken,  accessExpiry)  = _jwt.GenerateAccessToken(userDto);
        var (newRefresh,   refreshExpiry) = _jwt.GenerateRefreshToken();

        await _sessions.CreateAsync(
            userId, newRefresh, refreshExpiry, ipAddress, userAgent);

        return new LoginResponse
        {
            Success        = true,
            Token          = accessToken,
            RefreshToken   = newRefresh,
            TokenExpiresAt = accessExpiry,
            User           = userDto
        };
    }

    // ?? Logout ????????????????????????????????????????????????????????????????
    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var session = await _sessions.GetActiveByTokenAsync(refreshToken);
        if (session is null) return false;
        await _sessions.RevokeAsync(refreshToken);
        return true;
    }

    // ?? Helpers ???????????????????????????????????????????????????????????????
    private static LoginResponse Fail(string message) =>
        new() { Success = false, Message = message };
}

// ?? SystemLogManager ?????????????????????????????????????????????????????????
public class SystemLogManager : ISystemLogManager
{
    private readonly ISystemLogRepository _repo;
    public SystemLogManager(ISystemLogRepository repo) => _repo = repo;

    public Task<PagedResult<SystemLogDto>> GetSystemLogsAsync(SystemLogFilterRequest filter)
        => _repo.GetAllAsync(filter);
}

// ?? SettingsManager ???????????????????????????????????????????????????????????
public class SettingsManager : ISettingsManager
{
    private readonly SyslogDbContext _db;
    public SettingsManager(SyslogDbContext db) => _db = db;

    private static readonly PlatformSettingsDto _defaults = new()
    {
        PlatformName           = "SentinelLog Corp NOC",
        Organization           = "Acme Corporation",
        ContactEmail           = "noc@acme.corp",
        Timezone               = "UTC",
        DateFormat             = "YYYY-MM-DD HH:mm:ss",
        ShowMilliseconds       = true,
        AutoScrollFeed         = true,
        AudibleAlerts          = true,
        FeedBufferSize         = 5000,
        RefreshIntervalSeconds = 10
    };

    public async Task<PlatformSettingsDto> GetSettingsAsync()
    {
        var cfg = await GetCategoryConfigAsync("general");
        bool   Bool(string key, bool   def) => cfg.TryGetValue(key, out var v) ? v == "true" : def;
        int    Int (string key, int    def) => int.TryParse(cfg.GetValueOrDefault(key), out var n) ? n : def;
        string Str (string key, string def) => cfg.GetValueOrDefault(key) ?? def;

        return new PlatformSettingsDto
        {
            PlatformName           = Str ("PlatformName",           _defaults.PlatformName!),
            Organization           = Str ("Organization",           _defaults.Organization!),
            ContactEmail           = Str ("ContactEmail",           _defaults.ContactEmail!),
            Timezone               = Str ("Timezone",               _defaults.Timezone!),
            DateFormat             = Str ("DateFormat",             _defaults.DateFormat!),
            ShowMilliseconds       = Bool("ShowMilliseconds",       _defaults.ShowMilliseconds),
            AutoScrollFeed         = Bool("AutoScrollFeed",         _defaults.AutoScrollFeed),
            AudibleAlerts          = Bool("AudibleAlerts",          _defaults.AudibleAlerts),
            FeedBufferSize         = Int ("FeedBufferSize",         _defaults.FeedBufferSize),
            RefreshIntervalSeconds = Int ("RefreshIntervalSeconds", _defaults.RefreshIntervalSeconds)
        };
    }

    public async Task<PlatformSettingsDto> UpdateSettingsAsync(UpdatePlatformSettingsRequest req)
    {
        var current = await GetSettingsAsync();
        if (req.PlatformName           is not null) current.PlatformName           = req.PlatformName;
        if (req.Organization           is not null) current.Organization           = req.Organization;
        if (req.ContactEmail           is not null) current.ContactEmail           = req.ContactEmail;
        if (req.Timezone               is not null) current.Timezone               = req.Timezone;
        if (req.DateFormat             is not null) current.DateFormat             = req.DateFormat;
        if (req.ShowMilliseconds       is not null) current.ShowMilliseconds       = req.ShowMilliseconds.Value;
        if (req.AutoScrollFeed         is not null) current.AutoScrollFeed         = req.AutoScrollFeed.Value;
        if (req.AudibleAlerts          is not null) current.AudibleAlerts          = req.AudibleAlerts.Value;
        if (req.FeedBufferSize         is not null) current.FeedBufferSize         = req.FeedBufferSize.Value;
        if (req.RefreshIntervalSeconds is not null) current.RefreshIntervalSeconds = req.RefreshIntervalSeconds.Value;

        await SaveCategoryConfigAsync("general", new Dictionary<string, string?>
        {
            ["PlatformName"]           = current.PlatformName,
            ["Organization"]           = current.Organization,
            ["ContactEmail"]           = current.ContactEmail,
            ["Timezone"]               = current.Timezone,
            ["DateFormat"]             = current.DateFormat,
            ["ShowMilliseconds"]       = current.ShowMilliseconds.ToString().ToLower(),
            ["AutoScrollFeed"]         = current.AutoScrollFeed.ToString().ToLower(),
            ["AudibleAlerts"]          = current.AudibleAlerts.ToString().ToLower(),
            ["FeedBufferSize"]         = current.FeedBufferSize.ToString(),
            ["RefreshIntervalSeconds"] = current.RefreshIntervalSeconds.ToString()
        });
        return current;
    }

    public async Task<StorageOverviewDto> GetStorageOverviewAsync()
    {
        var totalEntries  = await _db.LogEvents.CountAsync();
        var estimatedGb   = totalEntries * 0.0005;

        var oneHourAgo    = DateTimeOffset.UtcNow.AddHours(-1);
        var lastHourCount = await _db.LogEvents.CountAsync(e => e.EventTime >= oneHourAgo);
        var ingestionGbH  = Math.Round(lastHourCount * 0.0005, 2);

        return new StorageOverviewDto
        {
            PrimaryStorageUsedTb   = Math.Round(estimatedGb / 1024, 4),
            PrimaryStorageTotalTb  = 3.5,
            ArchiveStorageUsedTb   = Math.Round(estimatedGb * 0.8 / 1024, 4),
            ArchiveStorageTotalTb  = 50.0,
            IngestionRateGbPerHour = ingestionGbH,
            CompressionRatio       = 8.3
        };
    }

    public async Task<List<StoragePolicyDto>> GetStoragePoliciesAsync()
    {
        return await _db.StoragePolicies.AsNoTracking()
            .Select(p => new StoragePolicyDto
            {
                Id            = p.Id,
                SourceType    = p.SourceType,
                RetentionDays = p.RetentionDays,
                Compression   = p.Compression,
                CreatedAt     = p.CreatedAt,
                UpdatedAt     = p.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<StoragePolicyDto> UpsertStoragePolicyAsync(UpsertStoragePolicyRequest req)
    {
        var entity = await _db.StoragePolicies
            .FirstOrDefaultAsync(p => p.SourceType == req.SourceType);

        if (entity is null)
        {
            entity = new StoragePolicy
            {
                SourceType    = req.SourceType,
                RetentionDays = req.RetentionDays,
                Compression   = req.Compression,
                CreatedAt     = DateTimeOffset.UtcNow
            };
            _db.StoragePolicies.Add(entity);
        }
        else
        {
            entity.RetentionDays = req.RetentionDays;
            entity.Compression   = req.Compression;
            entity.UpdatedAt     = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        return new StoragePolicyDto
        {
            Id            = entity.Id,
            SourceType    = entity.SourceType,
            RetentionDays = entity.RetentionDays,
            Compression   = entity.Compression,
            CreatedAt     = entity.CreatedAt,
            UpdatedAt     = entity.UpdatedAt
        };
    }

    public async Task<List<SystemMetricDto>> GetMetricsAsync(string? name, int lastMinutes)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-lastMinutes);

        var q = _db.SystemMetrics.AsNoTracking()
            .Where(m => m.MetricTime >= cutoff);

        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(m => m.Name == name);

        return await q
            .OrderBy(m => m.MetricTime)
            .Select(m => new SystemMetricDto
            {
                Id         = m.Id,
                Name       = m.Name,
                Value      = m.Value,
                MetricTime = m.MetricTime
            })
            .ToListAsync();
    }

    // #66/#67 — storage history (event count per day + estimated MB)
    public async Task<List<StorageHistoryDto>> GetStorageHistoryAsync(int days)
    {
        var now  = DateTimeOffset.UtcNow;
        var from = now.AddDays(-days);

        var timestamps = await _db.LogEvents.AsNoTracking()
            .Where(l => l.EventTime >= from)
            .Select(l => l.EventTime)
            .ToListAsync();

        return Enumerable.Range(0, days)
            .Select(i =>
            {
                var day = from.AddDays(i).Date;
                var cnt = timestamps.Count(t => t.Date == day);
                return new StorageHistoryDto
                {
                    Date        = day.ToString("yyyy-MM-dd"),
                    EventCount  = cnt,
                    EstimatedMb = Math.Round(cnt * 0.0005 * 1024, 2)   // 0.5 KB/event → MB
                };
            })
            .ToList();
    }

    public async Task<bool> DeleteStoragePolicyAsync(long id)
    {
        var entity = await _db.StoragePolicies.FindAsync(id);
        if (entity is null) return false;
        entity.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // Config helpers for platformconfig table
    private async Task<string?> GetConfigAsync(string category, string key)
    {
        var row = await _db.PlatformConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Category == category && c.Key == key);
        return row?.Value;
    }

    private async Task SetConfigAsync(string category, string key, string? value)
    {
        var row = await _db.PlatformConfigs
            .FirstOrDefaultAsync(c => c.Category == category && c.Key == key);
        if (row is null)
        {
            _db.PlatformConfigs.Add(new DRS.Scaffold.Syslog.Core.Models.PlatformConfig
            {
                Category  = category,
                Key       = key,
                Value     = value,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.Value     = value;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    private async Task<Dictionary<string, string?>> GetCategoryConfigAsync(string category)
    {
        var rows = await _db.PlatformConfigs.AsNoTracking()
            .Where(c => c.Category == category)
            .ToListAsync();
        return rows.ToDictionary(r => r.Key ?? "", r => r.Value);
    }

    private async Task SaveCategoryConfigAsync(string category, Dictionary<string, string?> values)
    {
        var keys     = values.Keys.ToList();
        var existing = await _db.PlatformConfigs
            .Where(c => c.Category == category && c.Key != null && keys.Contains(c.Key))
            .ToDictionaryAsync(c => c.Key!);

        foreach (var (key, val) in values)
        {
            if (existing.TryGetValue(key, out var row))
            {
                row.Value     = val;
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.PlatformConfigs.Add(new DRS.Scaffold.Syslog.Core.Models.PlatformConfig
                {
                    Category  = category,
                    Key       = key,
                    Value     = val,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    // #86-90 — network settings
    public async Task<NetworkSettingsDto> GetNetworkSettingsAsync()
    {
        var cfg = await GetCategoryConfigAsync("network");
        return new NetworkSettingsDto
        {
            UdpPort        = int.TryParse(cfg.GetValueOrDefault("UdpPort"),        out var udp)  ? udp  : 514,
            TcpPort        = int.TryParse(cfg.GetValueOrDefault("TcpPort"),        out var tcp)  ? tcp  : 514,
            TlsPort        = int.TryParse(cfg.GetValueOrDefault("TlsPort"),        out var tls)  ? tls  : 6514,
            TlsEnabled     = cfg.GetValueOrDefault("TlsEnabled") == "true",
            TlsCertPath    = cfg.GetValueOrDefault("TlsCertPath"),
            AllowedSubnets = cfg.GetValueOrDefault("AllowedSubnets"),
            MaxConnections = int.TryParse(cfg.GetValueOrDefault("MaxConnections"), out var mc)   ? mc   : null
        };
    }

    public async Task<NetworkSettingsDto> SaveNetworkSettingsAsync(NetworkSettingsDto dto)
    {
        await SaveCategoryConfigAsync("network", new Dictionary<string, string?>
        {
            ["UdpPort"]        = dto.UdpPort.ToString(),
            ["TcpPort"]        = dto.TcpPort.ToString(),
            ["TlsPort"]        = dto.TlsPort.ToString(),
            ["TlsEnabled"]     = dto.TlsEnabled.ToString().ToLower(),
            ["TlsCertPath"]    = dto.TlsCertPath,
            ["AllowedSubnets"] = dto.AllowedSubnets,
            ["MaxConnections"] = dto.MaxConnections?.ToString()
        });
        return dto;
    }

    // #86-90 — notification settings (SMTP)
    public async Task<NotificationSettingsDto> GetNotificationSettingsAsync()
    {
        var cfg = await GetCategoryConfigAsync("notifications");
        return new NotificationSettingsDto
        {
            SmtpHost    = cfg.GetValueOrDefault("SmtpHost"),
            SmtpPort    = int.TryParse(cfg.GetValueOrDefault("SmtpPort"), out var port) ? port : 587,
            SmtpTls     = cfg.GetValueOrDefault("SmtpTls") != "false",
            SmtpUser    = cfg.GetValueOrDefault("SmtpUser"),
            // SmtpPass intentionally not returned (write-only)
            FromAddress = cfg.GetValueOrDefault("FromAddress")
        };
    }

    public async Task<NotificationSettingsDto> SaveNotificationSettingsAsync(NotificationSettingsDto dto)
    {
        var values = new Dictionary<string, string?>
        {
            ["SmtpHost"]    = dto.SmtpHost,
            ["SmtpPort"]    = dto.SmtpPort.ToString(),
            ["SmtpTls"]     = dto.SmtpTls.ToString().ToLower(),
            ["SmtpUser"]    = dto.SmtpUser,
            ["FromAddress"] = dto.FromAddress
        };
        // Only overwrite stored password if a new one was provided
        if (!string.IsNullOrEmpty(dto.SmtpPass))
            values["SmtpPass"] = dto.SmtpPass;
        await SaveCategoryConfigAsync("notifications", values);
        return dto;
    }

    // #95 — LDAP settings
    public async Task<LdapSettingsDto> GetLdapSettingsAsync()
    {
        var cfg = await GetCategoryConfigAsync("ldap");
        return new LdapSettingsDto
        {
            Server   = cfg.GetValueOrDefault("Server"),
            Port     = int.TryParse(cfg.GetValueOrDefault("Port"), out var port) ? port : 389,
            UseTls   = cfg.GetValueOrDefault("UseTls") == "true",
            BaseDn   = cfg.GetValueOrDefault("BaseDn"),
            BindDn   = cfg.GetValueOrDefault("BindDn"),
            // BindPass intentionally not returned (write-only)
            UserAttr = cfg.GetValueOrDefault("UserAttr"),
            Enabled  = cfg.GetValueOrDefault("Enabled") == "true"
        };
    }

    public async Task<LdapSettingsDto> SaveLdapSettingsAsync(LdapSettingsDto dto)
    {
        var values = new Dictionary<string, string?>
        {
            ["Server"]   = dto.Server,
            ["Port"]     = dto.Port.ToString(),
            ["UseTls"]   = dto.UseTls.ToString().ToLower(),
            ["BaseDn"]   = dto.BaseDn,
            ["BindDn"]   = dto.BindDn,
            ["UserAttr"] = dto.UserAttr,
            ["Enabled"]  = dto.Enabled.ToString().ToLower()
        };
        if (!string.IsNullOrEmpty(dto.BindPass))
            values["BindPass"] = dto.BindPass;
        await SaveCategoryConfigAsync("ldap", values);
        return dto;
    }

    // #96 — auth policy
    public async Task<AuthPolicyDto> GetAuthPolicyAsync()
    {
        var cfg = await GetCategoryConfigAsync("auth");
        return new AuthPolicyDto
        {
            SessionTimeoutMinutes  = int.TryParse(cfg.GetValueOrDefault("SessionTimeoutMinutes"),  out var st) ? st : 480,
            MaxLoginAttempts       = int.TryParse(cfg.GetValueOrDefault("MaxLoginAttempts"),       out var ml) ? ml : 5,
            LockoutDurationMinutes = int.TryParse(cfg.GetValueOrDefault("LockoutDurationMinutes"), out var ld) ? ld : 15,
            MfaRequired            = cfg.GetValueOrDefault("MfaRequired") == "true",
            MinPasswordLength      = int.TryParse(cfg.GetValueOrDefault("MinPasswordLength"),      out var pl) ? pl : 8
        };
    }

    public async Task<AuthPolicyDto> SaveAuthPolicyAsync(AuthPolicyDto dto)
    {
        await SaveCategoryConfigAsync("auth", new Dictionary<string, string?>
        {
            ["SessionTimeoutMinutes"]  = dto.SessionTimeoutMinutes.ToString(),
            ["MaxLoginAttempts"]       = dto.MaxLoginAttempts.ToString(),
            ["LockoutDurationMinutes"] = dto.LockoutDurationMinutes.ToString(),
            ["MfaRequired"]            = dto.MfaRequired.ToString().ToLower(),
            ["MinPasswordLength"]      = dto.MinPasswordLength.ToString()
        });
        return dto;
    }

    // #81 — SNMP trap forwarding
    public async Task<SnmpSettingsDto> GetSnmpSettingsAsync()
    {
        var cfg = await GetCategoryConfigAsync("snmp");
        return new SnmpSettingsDto
        {
            Enabled      = cfg.GetValueOrDefault("Enabled") == "true",
            Host         = cfg.GetValueOrDefault("Host"),
            Port         = int.TryParse(cfg.GetValueOrDefault("Port"), out var port) ? port : null,
            Community    = cfg.GetValueOrDefault("Community"),
            Version      = cfg.GetValueOrDefault("Version") ?? "v2c",
            AuthUser     = cfg.GetValueOrDefault("AuthUser"),
            AuthPassword = cfg.GetValueOrDefault("AuthPassword"),
            PrivPassword = cfg.GetValueOrDefault("PrivPassword")
        };
    }

    public async Task<SnmpSettingsDto> SaveSnmpSettingsAsync(SnmpSettingsDto dto)
    {
        await SaveCategoryConfigAsync("snmp", new Dictionary<string, string?>
        {
            ["Enabled"]      = dto.Enabled.ToString().ToLower(),
            ["Host"]         = dto.Host,
            ["Port"]         = dto.Port?.ToString(),
            ["Community"]    = dto.Community,
            ["Version"]      = dto.Version,
            ["AuthUser"]     = dto.AuthUser,
            ["AuthPassword"] = dto.AuthPassword,
            ["PrivPassword"] = dto.PrivPassword
        });
        return dto;
    }

    // Generic platformconfig access (#81 parsers + any custom category)
    public async Task<List<PlatformConfigDto>> GetPlatformConfigAsync(string category)
    {
        var rows = await _db.PlatformConfigs.AsNoTracking()
            .Where(c => c.Category == category)
            .ToListAsync();
        return rows.Select(r => new PlatformConfigDto { Category = r.Category ?? "", Key = r.Key ?? "", Value = r.Value }).ToList();
    }

    public async Task SetPlatformConfigAsync(SetPlatformConfigRequest request)
        => await SetConfigAsync(request.Category, request.Key, request.Value);

    // #79 — Scheduled reports (delegated to IScheduledReportRepository via DI)
    private IScheduledReportRepository? _reportRepo;

    private IScheduledReportRepository ReportRepo =>
        _reportRepo ??= new ScheduledReportRepository(_db);

    public Task<List<ScheduledReportDto>> GetScheduledReportsAsync() =>
        ReportRepo.GetAllAsync();

    public Task<ScheduledReportDto> CreateScheduledReportAsync(CreateScheduledReportRequest req) =>
        ReportRepo.CreateAsync(req);

    public Task<bool> DeleteScheduledReportAsync(long id) =>
        ReportRepo.DeleteAsync(id);

    public Task<bool> ToggleScheduledReportAsync(long id, bool enabled) =>
        ReportRepo.ToggleAsync(id, enabled);
}
