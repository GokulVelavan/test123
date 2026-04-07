using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class LogEntryRepository : ILogEntryRepository
{
    private readonly SyslogDbContext _db;
    public LogEntryRepository(SyslogDbContext db) => _db = db;

    public async Task<PagedResult<LogEventDto>> SearchAsync(LogSearchRequest req)
    {
        var q = _db.LogEvents.Include(l => l.Source).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Keyword))
            q = q.Where(l => l.Message != null && l.Message.Contains(req.Keyword));
        if (!string.IsNullOrWhiteSpace(req.Hostname))
            q = q.Where(l => l.Hostname != null && l.Hostname.Contains(req.Hostname));
        if (req.Severity.HasValue)
            q = q.Where(l => l.Severity == req.Severity.Value);
        if (req.Facility.HasValue)
            q = q.Where(l => l.Facility == req.Facility.Value);
        if (!string.IsNullOrWhiteSpace(req.DeviceType))
            q = q.Where(l => l.DeviceType == req.DeviceType);
        if (!string.IsNullOrWhiteSpace(req.Program))
            q = q.Where(l => l.Program != null && l.Program.Contains(req.Program));
        if (!string.IsNullOrWhiteSpace(req.SourceIp))
            q = q.Where(l => l.Source != null && l.Source.IpAddress != null && l.Source.IpAddress.Contains(req.SourceIp));
        if (req.TimeFrom.HasValue)
            q = q.Where(l => l.EventTime >= req.TimeFrom.Value);
        if (req.TimeTo.HasValue)
            q = q.Where(l => l.EventTime <= req.TimeTo.Value);

        var total = await q.CountAsync();
        q = req.Descending
            ? q.OrderByDescending(l => l.ReceivedTime).ThenByDescending(l => l.EventTime)
            : q.OrderBy(l => l.ReceivedTime).ThenBy(l => l.EventTime);

        var page     = Math.Max(1, req.Page);
        var pageSize = Math.Max(1, Math.Min(req.PageSize, 1000));
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<LogEventDto>
        {
            Items = items.Select(ToDto).ToList(), TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<List<LogEventDto>> GetLiveAsync(LiveLogQueryRequest req)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-req.LastSeconds);
        var q = _db.LogEvents.AsNoTracking()
            .Where(l => l.ReceivedTime >= cutoff);

        if (req.Severity.HasValue)
            q = q.Where(l => l.Severity == req.Severity.Value);
        if (!string.IsNullOrWhiteSpace(req.DeviceType))
            q = q.Where(l => l.DeviceType == req.DeviceType);

        return await q
            .OrderByDescending(l => l.ReceivedTime)
            .Take(req.Limit)
            .Select(l => ToDto(l))
            .ToListAsync();
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        var last24h   = DateTimeOffset.UtcNow.AddHours(-24);
        var hourAgo   = DateTimeOffset.UtcNow.AddHours(-1);
        var minuteAgo = DateTimeOffset.UtcNow.AddMinutes(-1);

        var totalToday    = await _db.LogEvents.CountAsync(l => l.EventTime >= last24h);
        var critAlerts    = await _db.AlertEvents.CountAsync(a => a.Severity == "Critical" && !a.Acknowledged);
        var errorsPerHr   = await _db.LogEvents.CountAsync(l => l.Severity == 3 && l.ReceivedTime >= hourAgo);
        var warningsToday = await _db.LogEvents.CountAsync(l => l.Severity == 4 && l.EventTime >= last24h);
        var activeSrc     = await _db.Sources.CountAsync(s => s.Status == "Online");
        var eventsLastMin = await _db.LogEvents.CountAsync(l => l.ReceivedTime >= minuteAgo);

        return new DashboardSummaryDto
        {
            TotalEventsToday = totalToday,
            CriticalAlerts   = critAlerts,
            ErrorsPerHour    = errorsPerHr,
            Warnings         = warningsToday,
            ActiveSources    = activeSrc,
            CurrentEps       = (decimal)eventsLastMin / 60
        };
    }

    public async Task<List<EventVolumeDto>> GetEventVolumeAsync(int hours, int bucketMinutes)
    {
        var from = DateTimeOffset.UtcNow.AddHours(-hours);
        var entries = await _db.LogEvents
            .AsNoTracking()
            .Where(l => l.EventTime >= from)
            .Select(l => new { l.EventTime, l.Severity })
            .ToListAsync();

        return entries
            .GroupBy(l =>
            {
                var slot = (long)(l.EventTime - from).TotalMinutes / bucketMinutes;
                return from.AddMinutes(slot * bucketMinutes);
            })
            .Select(g => new EventVolumeDto
            {
                BucketTime = g.Key,
                Critical   = g.Count(l => l.Severity <= 2),
                Error      = g.Count(l => l.Severity == 3),
                Warning    = g.Count(l => l.Severity == 4),
                Info       = g.Count(l => l.Severity is 5 or 6),
                Debug      = g.Count(l => l.Severity == 7),
                Total      = g.Count()
            })
            .OrderBy(b => b.BucketTime)
            .ToList();
    }

    public async Task<SeverityBreakdownDto> GetSeverityBreakdownAsync(DateTimeOffset? from, DateTimeOffset? to)
    {
        var q = _db.LogEvents.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(l => l.EventTime >= from.Value);
        if (to.HasValue)   q = q.Where(l => l.EventTime <= to.Value);

        var counts = await q
            .GroupBy(l => l.Severity)
            .Select(g => new { Severity = g.Key, Count = (long)g.Count() })
            .ToListAsync();

        var dto = new SeverityBreakdownDto();
        foreach (var c in counts)
        {
            switch (c.Severity)
            {
                case <= 2: dto.Critical += c.Count; break;
                case 3:    dto.Error    += c.Count; break;
                case 4:    dto.Warning  += c.Count; break;
                case 5:    dto.Notice   += c.Count; break;
                case 6:    dto.Info     += c.Count; break;
                case 7:    dto.Debug    += c.Count; break;
            }
        }
        dto.Total = dto.Critical + dto.Error + dto.Warning + dto.Notice + dto.Info + dto.Debug;
        return dto;
    }

    public async Task<List<TopTalkerDto>> GetTopTalkersAsync(int topN, DateTimeOffset? from, DateTimeOffset? to)
    {
        var q = _db.LogEvents.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(l => l.EventTime >= from.Value);
        if (to.HasValue)   q = q.Where(l => l.EventTime <= to.Value);

        var results = await q
            .GroupBy(l => new { l.Hostname, l.DeviceType })
            .Select(g => new
            {
                g.Key.Hostname,
                g.Key.DeviceType,
                Count = (long)g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync();

        return results.Select((x, i) => new TopTalkerDto
        {
            Rank       = i + 1,
            Host       = x.Hostname ?? "unknown",
            DeviceType = x.DeviceType,
            EventCount = x.Count
        }).ToList();
    }

    // #41 — facet sidebar: hostname, severity, deviceType, program
    public async Task<List<LogFacetDto>> GetFacetsAsync(LogSearchRequest filter)
    {
        var q = _db.LogEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            q = q.Where(l => l.Message != null && l.Message.Contains(filter.Keyword));
        if (filter.TimeFrom.HasValue) q = q.Where(l => l.EventTime >= filter.TimeFrom.Value);
        if (filter.TimeTo.HasValue)   q = q.Where(l => l.EventTime <= filter.TimeTo.Value);

        var entries = await q.Select(l => new { l.Hostname, l.Severity, l.DeviceType, l.Program }).ToListAsync();

        var severityLabels = new Dictionary<short, string>
        {
            { 0, "Emergency" }, { 1, "Alert" }, { 2, "Critical" }, { 3, "Error" },
            { 4, "Warning" }, { 5, "Notice" }, { 6, "Info" }, { 7, "Debug" }
        };

        return new List<LogFacetDto>
        {
            new() { Field = "hostname", Items = entries.Where(e => e.Hostname != null)
                .GroupBy(e => e.Hostname!).OrderByDescending(g => g.Count()).Take(10)
                .Select(g => new FacetItem { Value = g.Key, Count = g.Count() }).ToList() },
            new() { Field = "severity", Items = entries.Where(e => e.Severity.HasValue)
                .GroupBy(e => e.Severity!.Value).OrderBy(g => g.Key)
                .Select(g => new FacetItem { Value = severityLabels.GetValueOrDefault(g.Key, g.Key.ToString()), Count = g.Count() }).ToList() },
            new() { Field = "deviceType", Items = entries.Where(e => e.DeviceType != null)
                .GroupBy(e => e.DeviceType!).OrderByDescending(g => g.Count()).Take(10)
                .Select(g => new FacetItem { Value = g.Key, Count = g.Count() }).ToList() },
            new() { Field = "program", Items = entries.Where(e => e.Program != null)
                .GroupBy(e => e.Program!).OrderByDescending(g => g.Count()).Take(10)
                .Select(g => new FacetItem { Value = g.Key, Count = g.Count() }).ToList() }
        };
    }

    // #51 — activity heatmap: day-of-week × hour-of-day buckets
    public async Task<List<HeatmapBucketDto>> GetHeatmapAsync(int days)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-days);
        var times = await _db.LogEvents.AsNoTracking()
            .Where(l => l.EventTime >= from)
            .Select(l => l.EventTime)
            .ToListAsync();

        return times
            .GroupBy(t => new { Day = (int)t.DayOfWeek, Hour = t.Hour })
            .Select(g => new HeatmapBucketDto
            {
                Day        = g.Key.Day,
                Hour       = g.Key.Hour,
                EventCount = g.Count()
            })
            .ToList();
    }

    // #52 — security category breakdown based on message keyword patterns
    public async Task<List<CategoryBreakdownDto>> GetCategoryBreakdownAsync(int days)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-days);
        var messages = await _db.LogEvents.AsNoTracking()
            .Where(l => l.EventTime >= from && l.Message != null)
            .Select(l => l.Message!)
            .ToListAsync();

        var categories = new Dictionary<string, string[]>
        {
            ["Authentication"] = new[] { "login", "logout", "auth", "password", "credential", "ssh", "sudo" },
            ["Network"]        = new[] { "firewall", "deny", "permit", "tcp", "udp", "port", "connection" },
            ["Malware"]        = new[] { "virus", "trojan", "malware", "ransomware", "exploit", "payload" },
            ["System"]         = new[] { "kernel", "cpu", "memory", "disk", "service", "process", "startup" },
            ["Access Control"] = new[] { "permission", "denied", "unauthorized", "forbidden", "privilege" },
            ["Other"]          = Array.Empty<string>()
        };

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var cat in categories.Keys) result[cat] = 0;

        foreach (var msg in messages)
        {
            var lower = msg.ToLowerInvariant();
            bool matched = false;
            foreach (var (cat, keywords) in categories.Where(kv => kv.Value.Length > 0))
            {
                if (keywords.Any(k => lower.Contains(k))) { result[cat]++; matched = true; break; }
            }
            if (!matched) result["Other"]++;
        }

        return result
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new CategoryBreakdownDto { Category = kv.Key, EventCount = kv.Value })
            .ToList();
    }

    private static readonly string[] _sevLabels =
        { "emergency", "alert", "critical", "error", "warning", "notice", "info", "debug" };

    private static readonly string[] _facLabels =
        { "kern", "user", "mail", "daemon", "auth", "syslog", "lpr", "news",
          "uucp", "cron", "authpriv", "ftp", "ntp", "audit", "alert", "clock",
          "local0", "local1", "local2", "local3", "local4", "local5", "local6", "local7" };

    private static LogEventDto ToDto(LogEvent l) => new()
    {
        Id            = l.Id,
        SourceId      = l.SourceId,
        Hostname      = l.Hostname,
        Facility      = l.Facility,
        Severity      = l.Severity,
        Program       = l.Program,
        Message       = l.Message,
        RawMessage    = l.RawMessage,
        EventCode     = l.EventCode,
        DeviceType    = l.DeviceType,
        EventTime     = l.EventTime,
        ReceivedTime  = l.ReceivedTime,
        SourceIp      = l.Source?.IpAddress,
        SeverityLabel = l.Severity is >= 0 and <= 7 ? _sevLabels[l.Severity.Value] : "info",
        FacilityLabel = l.Facility is >= 0 and <= 23 ? _facLabels[l.Facility.Value] : "unknown"
    };
}
