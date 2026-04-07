using DRS.Scaffold.Syslog.WebUI.Models;
using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

/// <summary>Maps to syslog-dashboard.html � Overview, Live Feed, Search, Alerts, Storage, Analytics.</summary>
[Route("dashboard")]
public class DashboardController : AuthenticatedController
{
    public DashboardController(ApiClient api) : base(api) { }

    // GET /dashboard   � Overview screen
    [HttpGet("")]
    [HttpGet("overview")]
    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel { FetchedAt = DateTimeOffset.UtcNow };

        // Fire all requests in parallel
        var summaryTask  = Api.GetDashboardSummaryAsync();
        var sourcesTask  = Api.GetSourcesSummaryAsync();
        var alertsTask   = Api.GetAlertEventsAsync(page: 1, pageSize: 10);
        var talkersTask  = Api.GetTopTalkersAsync(topN: 5);
        var volumeTask   = Api.GetEventVolumeAsync(hours: 48, bucketMinutes: 60);

        await Task.WhenAll(summaryTask, sourcesTask, alertsTask, talkersTask, volumeTask);

        // ?? KPI metrics ???????????????????????????????????????????????????????
        if (summaryTask.Result is { } summary)
        {
            vm.TotalEvents24h = summary.TotalEventsToday;
            vm.CriticalAlerts = summary.CriticalAlerts;
            vm.ErrorsPerHour  = summary.ErrorsPerHour;
            vm.Warnings       = summary.Warnings;
            vm.ActiveSources  = summary.ActiveSources;
            vm.EpsNow         = summary.CurrentEps;
        }

        if (sourcesTask.Result is { } sources)
        {
            vm.ActiveSources  = sources.OnlineCount;
            vm.OfflineSources = sources.OfflineCount + sources.DegradedCount;
        }

        // ?? Trend indicators: compare today (last 24 buckets) vs yesterday (first 24 buckets)
        if (volumeTask.Result is { Count: >= 2 } vol)
        {
            var half      = vol.Count / 2;
            var yesterday = vol.Take(half).ToList();
            var today     = vol.Skip(half).ToList();

            static double? Pct(long cur, long prev) =>
                prev > 0 ? Math.Round((cur - prev) * 100.0 / prev, 1) : null;

            var todayTotal     = today.Sum(b => (long)(b.Critical + b.Error + b.Warning + b.Info));
            var yesterdayTotal = yesterday.Sum(b => (long)(b.Critical + b.Error + b.Warning + b.Info));
            vm.TrendEvents = Pct(todayTotal, yesterdayTotal);

            var todayErr     = today.Sum(b => (long)b.Error);
            var yesterdayErr = yesterday.Sum(b => (long)b.Error);
            vm.TrendErrors = Pct(todayErr, yesterdayErr);

            var todayWarn     = today.Sum(b => (long)b.Warning);
            var yesterdayWarn = yesterday.Sum(b => (long)b.Warning);
            vm.TrendWarnings = Pct(todayWarn, yesterdayWarn);
        }

        // ?? Recent alerts ?????????????????????????????????????????????????????
        if (alertsTask.Result?.Items is { Count: > 0 } alertItems)
        {
            vm.RecentAlerts = alertItems.Select(a =>
            {
                var msg = a.Message ?? string.Empty;
                var hostMatch = System.Text.RegularExpressions.Regex.Match(msg, @"on host '([^']+)'");
                var source = hostMatch.Success ? hostMatch.Groups[1].Value : (a.RuleName ?? string.Empty);
                return new RecentAlertRow
                {
                    Time     = (a.FiredAt ?? a.CreatedAt).ToLocalTime().ToString("HH:mm:ss"),
                    Severity = a.Severity ?? "info",
                    Source   = source,
                    RuleName = a.RuleName ?? string.Empty,
                    Message  = msg,
                    Status   = a.Acknowledged ? "RESOLVED" : "ACTIVE",
                };
            }).ToList();
        }

        // ?? Top sources ???????????????????????????????????????????????????????
        if (talkersTask.Result is { Count: > 0 } talkers)
        {
            var maxEvents = talkers.Max(t => t.EventCount);
            var colors = new[]
            {
                "var(--sev-critical)", "var(--sev-info)", "var(--sev-notice)",
                "var(--sev-warning)",  "var(--sev-error)"
            };

            vm.TopSources = talkers.Select((t, i) => new TopSourceRow
            {
                Source   = t.Host,
                Ip       = string.Empty,
                Type     = t.DeviceType ?? string.Empty,
                Events   = (int)Math.Min(t.EventCount, int.MaxValue),
                PctOfMax = maxEvents > 0
                    ? (int)Math.Round(t.EventCount * 100.0 / maxEvents)
                    : 0,
                Color = colors[i % colors.Length],
            }).ToList();
        }

        ViewData["TopbarMeta"] = $"Last updated: {vm.FetchedAt.ToLocalTime():HH:mm:ss}";
        return View(vm);
    }

    // GET /dashboard/livefeed
    [HttpGet("livefeed")]
    public IActionResult LiveFeed() => View();

    // Severity name → RFC 5424 numeric value
    private static readonly Dictionary<string, string> _severityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Emergency"] = "0", ["Alert"] = "1", ["Critical"] = "2", ["Error"] = "3",
        ["Warning"]   = "4", ["Notice"] = "5", ["Info"]    = "6", ["Debug"]  = "7"
    };

    // Facility name → RFC 5424 numeric value
    private static readonly Dictionary<string, string> _facilityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kern"] = "0", ["user"] = "1", ["mail"] = "2", ["daemon"] = "3",
        ["auth"] = "4", ["syslog"] = "5", ["lpr"] = "6", ["news"] = "7",
        ["uucp"] = "8", ["cron"] = "9", ["local0"] = "16", ["local1"] = "17",
        ["local2"] = "18", ["local3"] = "19", ["local4"] = "20", ["local5"] = "21",
        ["local6"] = "22", ["local7"] = "23"
    };

    // GET /dashboard/search
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        string? host, string? program, string? sourceIp,
        string? dateFrom, string? dateTo,
        string? keyword, string? severity, string? facility, string? deviceType,
        string? sortOrder = "desc",
        int page = 1, int pageSize = 50)
    {
        var vm = new LogSearchViewModel
        {
            Host = host, Program = program, SourceIp = sourceIp,
            DateFrom = dateFrom, DateTo = dateTo,
            Keyword = keyword, Severity = severity, Facility = facility, DeviceType = deviceType,
            SortOrder = sortOrder ?? "desc",
            Page = page, PageSize = pageSize
        };

        if (HttpContext.Request.QueryString.HasValue)
        {
            // Convert severity/facility display names to RFC 5424 numeric strings
            var severityNum = severity is not null && _severityMap.TryGetValue(severity, out var sv) ? sv : null;
            var facilityNum = facility is not null && _facilityMap.TryGetValue(facility, out var fv) ? fv : null;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await Api.GetLogsAsync(
                host, program, sourceIp, dateFrom, dateTo, page, pageSize,
                keyword, severityNum, facilityNum, deviceType, sortOrder);
            sw.Stop();
            vm.QueryTimeMs = sw.ElapsedMilliseconds;

            if (result is not null)
            {
                vm.Results = result.Items.Select(r => new SystemLogRow
                {
                    Id            = r.Id,
                    LogDatetime   = r.LogDatetime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ReceivedTime  = r.ReceivedTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Host          = r.Host,
                    Program       = r.Program,
                    Pid           = null,
                    Message       = r.Message,
                    SourceIp      = r.SourceIp,
                    SeverityLabel = r.SeverityLabel,
                    FacilityLabel = r.FacilityLabel
                }).ToList();
                vm.TotalCount = result.TotalCount;
            }
        }
        return View(vm);
    }

    // GET /dashboard/alerts
    [HttpGet("alerts")]
    public IActionResult Alerts() => View();

    // GET /dashboard/storage
    [HttpGet("storage")]
    public async Task<IActionResult> Storage()
    {
        var vm       = new StorageViewModel();
        var overview = await Api.GetStorageOverviewAsync();
        var policies = await Api.GetStoragePoliciesAsync();

        if (overview is not null)
        {
            vm.PrimaryStorageUsedTb   = overview.PrimaryStorageUsedTb;
            vm.PrimaryStorageTotalTb  = overview.PrimaryStorageTotalTb;
            vm.ArchiveStorageUsedTb   = overview.ArchiveStorageUsedTb;
            vm.ArchiveStorageTotalTb  = overview.ArchiveStorageTotalTb;
            vm.IngestionRateGbPerHour = overview.IngestionRateGbPerHour;
            vm.CompressionRatio       = overview.CompressionRatio;
        }

        vm.Policies = policies?.Select(p => new StoragePolicyRow
        {
            Id            = p.Id,
            SourceType    = p.SourceType,
            RetentionDays = p.RetentionDays,
            Compression   = p.Compression,
            CreatedAt     = p.CreatedAt.ToString("yyyy-MM-dd")
        }).ToList() ?? new();

        return View(vm);
    }

    // GET /dashboard/analytics
    [HttpGet("analytics")]
    public IActionResult Analytics() => View();
}
