using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>Platform settings, storage policies, system metrics and system logs.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsManager  _settings;
    private readonly ISystemLogManager _syslog;
    private readonly SyslogDbContext   _db;

    public SettingsController(ISettingsManager settings, ISystemLogManager syslog, SyslogDbContext db)
    {
        _settings = settings;
        _syslog   = syslog;
        _db       = db;
    }

    /// <summary>Get all platform settings (platform name, timezone, display preferences).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PlatformSettingsDto), 200)]
    public async Task<IActionResult> Get()
        => Ok(await _settings.GetSettingsAsync());

    /// <summary>Partial-update platform settings � only non-null fields are applied.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PlatformSettingsDto), 200)]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformSettingsRequest request)
        => Ok(await _settings.UpdateSettingsAsync(request));

    /// <summary>Storage tier usage derived from live <c>logevent</c> row count.</summary>
    [HttpGet("storage")]
    [ProducesResponseType(typeof(StorageOverviewDto), 200)]
    public async Task<IActionResult> GetStorage()
        => Ok(await _settings.GetStorageOverviewAsync());

    /// <summary>All per-source-type retention and compression policies from the <c>storagepolicy</c> table.</summary>
    [HttpGet("storage/policies")]
    [ProducesResponseType(typeof(List<StoragePolicyDto>), 200)]
    public async Task<IActionResult> GetStoragePolicies()
        => Ok(await _settings.GetStoragePoliciesAsync());

    /// <summary>Create or update a storage policy for a given <c>sourceType</c>.</summary>
    [HttpPut("storage/policies")]
    [ProducesResponseType(typeof(StoragePolicyDto), 200)]
    public async Task<IActionResult> UpsertStoragePolicy([FromBody] UpsertStoragePolicyRequest request)
        => Ok(await _settings.UpsertStoragePolicyAsync(request));

    /// <summary>Soft-delete a retention policy by id.</summary>
    [HttpDelete("storage/policies/{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteStoragePolicy(long id)
    {
        var ok = await _settings.DeleteStoragePolicyAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Time-series platform health metrics from the <c>systemmetric</c> table.</summary>
    /// <param name="name">Metric name filter, e.g. <c>cpu_usage</c>, <c>eps_ingest</c> (optional).</param>
    /// <param name="lastMinutes">Look-back window in minutes (default 60).</param>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(List<SystemMetricDto>), 200)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] string? name        = null,
        [FromQuery] int     lastMinutes = 60)
        => Ok(await _settings.GetMetricsAsync(name, lastMinutes));

    /// <summary>Paginated entries from the <c>systemlogs</c> table � syslog daemon and platform log records.</summary>
    [HttpGet("system-logs")]
    [ProducesResponseType(typeof(PagedResult<SystemLogDto>), 200)]
    public async Task<IActionResult> GetSystemLogs([FromQuery] SystemLogFilterRequest filter)
        => Ok(await _syslog.GetSystemLogsAsync(filter));

    // ── #66/#67 — Storage history ─────────────────────────────────────────────

    [HttpGet("storage/history")]
    [ProducesResponseType(typeof(List<StorageHistoryDto>), 200)]
    public async Task<IActionResult> GetStorageHistory([FromQuery] int days = 30)
        => Ok(await _settings.GetStorageHistoryAsync(days));

    // ── #86-90 — Network & Notification settings ──────────────────────────────

    [HttpGet("network")]
    [ProducesResponseType(typeof(NetworkSettingsDto), 200)]
    public async Task<IActionResult> GetNetwork() => Ok(await _settings.GetNetworkSettingsAsync());

    [HttpPut("network")]
    [ProducesResponseType(typeof(NetworkSettingsDto), 200)]
    public async Task<IActionResult> SaveNetwork([FromBody] NetworkSettingsDto dto)
        => Ok(await _settings.SaveNetworkSettingsAsync(dto));

    [HttpGet("notification-settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), 200)]
    public async Task<IActionResult> GetNotificationSettings() => Ok(await _settings.GetNotificationSettingsAsync());

    [HttpPut("notification-settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), 200)]
    public async Task<IActionResult> SaveNotificationSettings([FromBody] NotificationSettingsDto dto)
        => Ok(await _settings.SaveNotificationSettingsAsync(dto));

    public class SmtpTestRequest { public string? To { get; set; } }

    [HttpPost("smtp/test")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TestSmtp([FromBody] SmtpTestRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.To))
            return BadRequest("Recipient email is required.");

        var rows = await _db.PlatformConfigs
            .Where(c => c.Category == "notifications")
            .ToListAsync();
        var cfg = rows.ToDictionary(c => c.Key ?? "", c => c.Value ?? "", StringComparer.OrdinalIgnoreCase);

        var host = cfg.GetValueOrDefault("SmtpHost");
        if (string.IsNullOrWhiteSpace(host))
            return BadRequest("SMTP host is not configured. Save SMTP settings first.");

        var port = int.TryParse(cfg.GetValueOrDefault("SmtpPort"), out var p) ? p : 587;
        var tls  = cfg.GetValueOrDefault("SmtpTls", "true") != "false";
        var user = cfg.GetValueOrDefault("SmtpUser");
        var pass = cfg.GetValueOrDefault("SmtpPass");
        var from = cfg.GetValueOrDefault("FromAddress") ?? "sentinellog@localhost";

        try
        {
            using var smtp = new System.Net.Mail.SmtpClient(host, port) { EnableSsl = tls };
            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                smtp.Credentials = new System.Net.NetworkCredential(user, pass);

            var msg = new System.Net.Mail.MailMessage(from, req.To)
            {
                Subject    = "[SentinelLog] SMTP Test",
                Body       = "This is a test email from your SentinelLog platform. SMTP is configured correctly.",
                IsBodyHtml = false
            };
            await smtp.SendMailAsync(msg);
            return Ok(new { success = true, message = $"Test email sent to {req.To}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ── #95-96 — LDAP & Auth policy ──────────────────────────────────────────

    [HttpGet("ldap")]
    [ProducesResponseType(typeof(LdapSettingsDto), 200)]
    public async Task<IActionResult> GetLdap() => Ok(await _settings.GetLdapSettingsAsync());

    [HttpPut("ldap")]
    [ProducesResponseType(typeof(LdapSettingsDto), 200)]
    public async Task<IActionResult> SaveLdap([FromBody] LdapSettingsDto dto)
        => Ok(await _settings.SaveLdapSettingsAsync(dto));

    [HttpGet("auth-policy")]
    [ProducesResponseType(typeof(AuthPolicyDto), 200)]
    public async Task<IActionResult> GetAuthPolicy() => Ok(await _settings.GetAuthPolicyAsync());

    [HttpPut("auth-policy")]
    [ProducesResponseType(typeof(AuthPolicyDto), 200)]
    public async Task<IActionResult> SaveAuthPolicy([FromBody] AuthPolicyDto dto)
        => Ok(await _settings.SaveAuthPolicyAsync(dto));

    // ── Generic platformconfig read/write (parsers, custom) ──────────────────

    [HttpGet("platformconfig")]
    [ProducesResponseType(typeof(List<PlatformConfigDto>), 200)]
    public async Task<IActionResult> GetPlatformConfig([FromQuery] string category)
        => Ok(await _settings.GetPlatformConfigAsync(category));

    [HttpPost("platformconfig")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> SetPlatformConfig([FromBody] SetPlatformConfigRequest request)
    {
        await _settings.SetPlatformConfigAsync(request);
        return NoContent();
    }

    // ── #81 — SNMP trap forwarding ────────────────────────────────────────────

    [HttpGet("snmp")]
    [ProducesResponseType(typeof(SnmpSettingsDto), 200)]
    public async Task<IActionResult> GetSnmp() => Ok(await _settings.GetSnmpSettingsAsync());

    [HttpPut("snmp")]
    [ProducesResponseType(typeof(SnmpSettingsDto), 200)]
    public async Task<IActionResult> SaveSnmp([FromBody] SnmpSettingsDto dto)
        => Ok(await _settings.SaveSnmpSettingsAsync(dto));

    // ── Active Session Management ─────────────────────────────────────────────

    /// <summary>List all active (non-revoked, non-expired) user sessions.</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _db.UserSessions
            .Where(s => !s.IsDeleted && s.ExpiresAt > DateTimeOffset.UtcNow)
            .Include(s => s.User)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                UserId      = s.UserId,
                Username    = s.User != null ? s.User.Username : "—",
                FullName    = s.User != null ? s.User.FullName : null,
                s.IpAddress,
                s.UserAgent,
                s.CreatedAt,
                s.ExpiresAt
            })
            .ToListAsync();
        return Ok(sessions);
    }

    /// <summary>Revoke (terminate) an active session by ID.</summary>
    [HttpDelete("sessions/{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeSession(long id)
    {
        var session = await _db.UserSessions.FindAsync(id);
        if (session is null) return NotFound();
        session.IsDeleted  = true;
        session.ExpiresAt  = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Revoke all sessions for a specific user.</summary>
    [HttpDelete("sessions/user/{userId:long}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RevokeUserSessions(long userId)
    {
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .ToListAsync();
        foreach (var s in sessions)
        {
            s.IsDeleted = true;
            s.ExpiresAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
