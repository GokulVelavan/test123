using System.Text;
using System.Text.Json;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class ReportsManager : IReportsManager
{
    private readonly IScheduledReportRepository _repo;
    private readonly SyslogDbContext _db;

    public ReportsManager(IScheduledReportRepository repo, SyslogDbContext db)
    {
        _repo = repo;
        _db   = db;
    }

    public Task<List<ScheduledReportDto>> GetScheduledReportsAsync() => _repo.GetAllAsync();
    public Task<ScheduledReportDto>       CreateAsync(CreateScheduledReportRequest req) => _repo.CreateAsync(req);
    public Task<bool>                     DeleteAsync(long id) => _repo.DeleteAsync(id);
    public Task<bool>                     ToggleAsync(long id, bool enabled) => _repo.ToggleAsync(id, enabled);

    public async Task<ComplianceReportDto> GenerateComplianceReportAsync(string framework, int days)
    {
        var from   = DateTimeOffset.UtcNow.AddDays(-days);
        var events = await _db.LogEvents
            .Where(e => e.EventTime >= from)
            .ToListAsync();

        var cfg = GetFrameworkConfig(framework);

        var critical = events.Count(e => e.Severity <= cfg.CriticalSeverityMax);

        var authFail = events.Count(e => e.Message != null &&
            AuthKeywords.Any(k => e.Message.Contains(k, StringComparison.OrdinalIgnoreCase)));

        var policyViol = events.Count(e => e.Severity <= cfg.PolicySeverityMax &&
            e.Message != null &&
            PolicyKeywords.Any(k => e.Message.Contains(k, StringComparison.OrdinalIgnoreCase)));

        var recentCritical = events
            .Where(e => e.Severity <= cfg.CriticalSeverityMax)
            .OrderByDescending(e => e.EventTime)
            .Take(20)
            .Select(e => new ComplianceEventDto
            {
                Timestamp = e.EventTime,
                Host      = e.Hostname,
                Message   = e.Message,
                Severity  = e.Severity <= 0 ? "Emergency" : e.Severity == 1 ? "Alert" : "Critical"
            })
            .ToList();

        return new ComplianceReportDto
        {
            Framework        = framework,
            Period           = $"Last {days} days",
            GeneratedAt      = DateTimeOffset.UtcNow,
            TotalEvents      = events.Count,
            CriticalEvents   = critical,
            AuthFailures     = authFail,
            PolicyViolations = policyViol,
            CriticalLabel    = cfg.CriticalLabel,
            AuthLabel        = cfg.AuthLabel,
            PolicyLabel      = cfg.PolicyLabel,
            RecentCritical   = recentCritical
        };
    }

    private static readonly string[] AuthKeywords =
        ["authentication failure", "failed password", "login failed", "invalid password"];

    private static readonly string[] PolicyKeywords =
        ["denied", "blocked", "permission denied", "unauthorized", "violation"];

    private static FrameworkConfig GetFrameworkConfig(string framework) =>
        framework.ToLowerInvariant() switch
        {
            "pci-dss"  => new(2, 3, "Severity 0-2",              "Failed logins", "Access denied / blocked"),
            "hipaa"    => new(2, 3, "ePHI access events",       "Authentication failures", "Unauthorized data access"),
            "soc2"     => new(3, 3, "Severity 0-3 (incl. error)","Failed logins", "Denied / blocked"),
            "sox"      => new(2, 3, "Severity 0-2",              "Privileged account failures", "Policy violations / unauthorized changes"),
            "gdpr"     => new(2, 3, "Data breach indicators",    "Authentication failures", "Unauthorized PII access"),
            "nist"     => new(2, 3, "High-impact events (0-2)",  "Failed authentications", "Access control violations"),
            "nist-800-53" => new(2, 3, "High-impact events",    "Failed authentications", "Access control violations"),
            "iso27001" => new(2, 3, "Severity 0-2",              "Failed logins", "Denied / blocked / violation"),
            "nerc-cip" => new(1, 2, "Severity 0-1 (Emerg/Alert)","Failed logins", "Denied / unauthorized"),
            "cis"      => new(2, 3, "Critical/High severity",    "Login failures", "Unauthorized access attempts"),
            _          => new(2, 3, "Severity 0-2",              "Failed logins", "Denied / blocked")
        };

    private record FrameworkConfig(
        int    CriticalSeverityMax,
        int    PolicySeverityMax,
        string CriticalLabel,
        string AuthLabel,
        string PolicyLabel);

    public async Task<(byte[] Content, string ContentType, string FileName)?> RunReportAsync(long id)
    {
        var report = await _db.ScheduledReports
            .Where(r => r.Id == id && !r.IsDeleted)
            .FirstOrDefaultAsync();
        if (report is null) return null;

        var filters = ParseFilters(report.FiltersJson);
        var now     = DateTimeOffset.UtcNow;

        var query = _db.LogEvents.Where(e => !e.IsDeleted).AsQueryable();

        if (filters.TryGetValue("severity", out var sev) && short.TryParse(sev, out var sevNum))
            query = query.Where(e => e.Severity == sevNum);
        if (filters.TryGetValue("hostname", out var host) && !string.IsNullOrEmpty(host))
            query = query.Where(e => e.Hostname != null && e.Hostname.Contains(host));
        if (filters.TryGetValue("days", out var daysStr) && int.TryParse(daysStr, out var days))
            query = query.Where(e => e.EventTime >= now.AddDays(-days));
        else
            query = query.Where(e => e.EventTime >= now.AddDays(-7));

        var results = await query
            .OrderByDescending(e => e.EventTime)
            .Take(10_000)
            .Select(e => new { e.Id, e.EventTime, e.Hostname, e.Severity, e.Program, e.Message })
            .ToListAsync();

        var ts = now.ToString("yyyyMMdd_HHmmss");

        if (report.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            return (Encoding.UTF8.GetBytes(json), "application/json", $"report_{id}_{ts}.json");
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,EventTime,Hostname,Severity,Program,Message");
            foreach (var r in results)
                sb.AppendLine($"{r.Id},{r.EventTime:o},{EscapeCsv(r.Hostname)},{r.Severity},{EscapeCsv(r.Program)},{EscapeCsv(r.Message)}");
            var ext = report.Format.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? "csv" : report.Format;
            return (Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"report_{id}_{ts}.{ext}");
        }
    }

    private static Dictionary<string, string> ParseFilters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static string EscapeCsv(string? val) =>
        val is null ? "" : $"\"{val.Replace("\"", "\"\"")}\"";
}
