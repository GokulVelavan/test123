namespace DRS.Scaffold.Syslog.Core.ViewModels;

// ?? LogEvent ??????????????????????????????????????????????????????????????????

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
}

public class LogSearchRequest
{
    public string?   Keyword    { get; set; }
    public string?   Hostname   { get; set; }
    public short?    Severity   { get; set; }
    public short?    Facility   { get; set; }
    public string?   DeviceType { get; set; }
    public string?   Program    { get; set; }
    public string?   SourceIp   { get; set; }
    public DateTimeOffset? TimeFrom { get; set; }
    public DateTimeOffset? TimeTo   { get; set; }
    public int       Page       { get; set; } = 1;
    public int       PageSize   { get; set; } = 50;
    public bool      Descending { get; set; } = true;
}

public class LiveLogQueryRequest
{
    public int    LastSeconds { get; set; } = 60;
    public short? Severity    { get; set; }
    public string? DeviceType { get; set; }
    public int    Limit       { get; set; } = 200;
}

public class DashboardSummaryDto
{
    public long TotalEventsToday { get; set; }
    public int  CriticalAlerts   { get; set; }
    public int  ErrorsPerHour    { get; set; }
    public int  Warnings         { get; set; }
    public int  ActiveSources    { get; set; }
    public decimal CurrentEps    { get; set; }
}

public class EventVolumeDto
{
    public DateTimeOffset BucketTime { get; set; }
    public int            Critical   { get; set; }
    public int            Error      { get; set; }
    public int            Warning    { get; set; }
    public int            Info       { get; set; }
    public int            Debug      { get; set; }
    public int            Total      { get; set; }
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
    /// <summary>Alias used by WebUI — serialized as "host" in JSON.</summary>
    public string  Host       { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public long    EventCount { get; set; }
}
