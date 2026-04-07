namespace DRS.Scaffold.Syslog.Core.ViewModels;

// ?? LogSource ?????????????????????????????????????????????????????????????????

public class LogSourceDto
{
    public long    Id         { get; set; }
    public string? Name       { get; set; }
    public string? IpAddress  { get; set; }
    public string? DeviceType { get; set; }
    public string? OsType     { get; set; }
    public string? Location   { get; set; }
    /// <summary>Online | Offline | Degraded | Pending</summary>
    public string? Status     { get; set; }
    public DateTimeOffset? LastSeen  { get; set; }
    public DateTimeOffset  CreatedAt { get; set; }
    public string? CreatedBy  { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy  { get; set; }
    /// <summary>#73 — events per second calculated from logevent in the last 60 s.</summary>
    public int     CurrentEps { get; set; }
}

// #72 — bulk source operations
public class BulkSourceIdsRequest
{
    public List<long> Ids { get; set; } = new();
}

public class BulkSourceStatusRequest
{
    public List<long> Ids    { get; set; } = new();
    public string     Status { get; set; } = "Offline";
}

// #18/#22/#23 — per-source activity time-series
public class SourceActivityDto
{
    public DateTimeOffset BucketTime { get; set; }
    public int            EventCount { get; set; }
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

public class LogSourceSummaryDto
{
    public int TotalSources  { get; set; }
    public int OnlineCount   { get; set; }
    public int OfflineCount  { get; set; }
    public int DegradedCount { get; set; }
    public int PendingCount  { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
