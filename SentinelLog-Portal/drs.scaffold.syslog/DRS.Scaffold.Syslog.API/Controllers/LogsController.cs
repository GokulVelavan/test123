using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>Log event search, live feed, dashboard KPIs and analytics.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Logs")]
public class LogsController : ControllerBase
{
    private readonly ILogEntryManager _mgr;
    public LogsController(ILogEntryManager mgr) => _mgr = mgr;

    /// <summary>Dashboard headline KPIs � total events today, critical alerts, errors/hour, warnings, active sources.</summary>
    [HttpGet("dashboard/summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), 200)]
    public async Task<IActionResult> GetDashboardSummary()
        => Ok(await _mgr.GetDashboardSummaryAsync());

    /// <summary>Live log feed � events received in the last N seconds.</summary>
    /// <param name="lastSeconds">Window in seconds (default 60).</param>
    /// <param name="severity">Max RFC-5424 severity (0=Emergency � 7=Debug).</param>
    /// <param name="deviceType">Filter by device type, e.g. Firewall.</param>
    /// <param name="limit">Max rows returned (default 200).</param>
    [HttpGet("live")]
    [ProducesResponseType(typeof(List<LogEventDto>), 200)]
    public async Task<IActionResult> GetLiveFeed(
        [FromQuery] int     lastSeconds = 60,
        [FromQuery] short?  severity    = null,
        [FromQuery] string? deviceType  = null,
        [FromQuery] int     limit       = 200)
        => Ok(await _mgr.GetLiveFeedAsync(new LiveLogQueryRequest
        {
            LastSeconds = lastSeconds,
            Severity    = severity,
            DeviceType  = deviceType,
            Limit       = limit
        }));

    /// <summary>Paginated log search � keyword, hostname, severity, facility, deviceType, program, time range.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<LogEventDto>), 200)]
    public async Task<IActionResult> Search([FromQuery] LogSearchRequest request)
        => Ok(await _mgr.SearchLogsAsync(request));

    /// <summary>Same as GET /search but accepts the filter as a JSON request body.</summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(PagedResult<LogEventDto>), 200)]
    public async Task<IActionResult> SearchPost([FromBody] LogSearchRequest request)
        => Ok(await _mgr.SearchLogsAsync(request));

    /// <summary>Bucketed event counts by severity for the volume bar chart.</summary>
    /// <param name="hours">Look-back window in hours (default 24).</param>
    /// <param name="bucketMinutes">Bucket width in minutes (default 60).</param>
    [HttpGet("analytics/event-volume")]
    [ProducesResponseType(typeof(List<EventVolumeDto>), 200)]
    public async Task<IActionResult> GetEventVolume(
        [FromQuery] int hours         = 24,
        [FromQuery] int bucketMinutes = 60)
        => Ok(await _mgr.GetEventVolumeAsync(hours, bucketMinutes));

    /// <summary>Severity-level counts for the donut chart.</summary>
    /// <param name="from">Range start � ISO-8601 DateTimeOffset (optional).</param>
    /// <param name="to">Range end � ISO-8601 DateTimeOffset (optional).</param>
    [HttpGet("analytics/severity-breakdown")]
    [ProducesResponseType(typeof(SeverityBreakdownDto), 200)]
    public async Task<IActionResult> GetSeverityBreakdown(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to   = null)
        => Ok(await _mgr.GetSeverityBreakdownAsync(from, to));

    /// <summary>Top N hosts by event volume.</summary>
    /// <param name="topN">Number of results (default 10).</param>
    /// <param name="from">Range start (optional).</param>
    /// <param name="to">Range end (optional).</param>
    [HttpGet("analytics/top-talkers")]
    [ProducesResponseType(typeof(List<TopTalkerDto>), 200)]
    public async Task<IActionResult> GetTopTalkers(
        [FromQuery] int             topN = 10,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to   = null)
        => Ok(await _mgr.GetTopTalkersAsync(topN, from, to));

    /// <summary>#41 — Facet aggregations for search sidebar (hostname, severity, deviceType, program).</summary>
    [HttpGet("search/facets")]
    [ProducesResponseType(typeof(List<LogFacetDto>), 200)]
    public async Task<IActionResult> GetFacets([FromQuery] LogSearchRequest filter)
        => Ok(await _mgr.GetFacetsAsync(filter));

    /// <summary>#51 — Activity heatmap: event counts by day-of-week × hour-of-day.</summary>
    /// <param name="days">Look-back window in days (default 30).</param>
    [HttpGet("analytics/heatmap")]
    [ProducesResponseType(typeof(List<HeatmapBucketDto>), 200)]
    public async Task<IActionResult> GetHeatmap([FromQuery] int days = 30)
        => Ok(await _mgr.GetHeatmapAsync(days));

    /// <summary>#52 — Security category breakdown (Authentication, Network, Malware, etc.).</summary>
    /// <param name="days">Look-back window in days (default 7).</param>
    [HttpGet("analytics/categories")]
    [ProducesResponseType(typeof(List<CategoryBreakdownDto>), 200)]
    public async Task<IActionResult> GetCategories([FromQuery] int days = 7)
        => Ok(await _mgr.GetCategoryBreakdownAsync(days));
}
