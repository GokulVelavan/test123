namespace DRS.Scaffold.Syslog.Core.ViewModels;

// ── #41 Facet Sidebar ─────────────────────────────────────────────────────────

public class LogFacetDto
{
    public string          Field  { get; set; } = string.Empty;
    public List<FacetItem> Items  { get; set; } = new();
}

public class FacetItem
{
    public string Value { get; set; } = string.Empty;
    public long   Count { get; set; }
}

// ── #51 Activity Heatmap ──────────────────────────────────────────────────────

public class HeatmapBucketDto
{
    /// <summary>Day of week 0=Mon … 6=Sun</summary>
    public int Day        { get; set; }
    /// <summary>Hour of day 0-23 UTC</summary>
    public int Hour       { get; set; }
    public int EventCount { get; set; }
}

// ── #52 Security Category Breakdown ──────────────────────────────────────────

public class CategoryBreakdownDto
{
    public string Category   { get; set; } = string.Empty;
    public long   EventCount { get; set; }
}
