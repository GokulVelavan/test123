namespace DRS.Scaffold.Syslog.WebUI.Models;

// ?? Auth ??????????????????????????????????????????????????????????????????????
public class LoginViewModel
{
    public string  Username   { get; set; } = string.Empty;
    public string  Password   { get; set; } = string.Empty;
    public bool    RememberMe { get; set; }
    public string? Error      { get; set; }
}

// ?? Homepage (public landing page) ???????????????????????????????????????????
/// <summary>
/// Data passed to Home/Index.cshtml � all values sourced from the API at request
/// time.  Falls back to safe defaults so the page still renders if the API is
/// unreachable.
/// </summary>
public class HomepageViewModel
{
    // ?? Login form ????????????????????????????????????????????????????????????
    public string  Username   { get; set; } = string.Empty;
    public string  Password   { get; set; } = string.Empty;
    public bool    RememberMe { get; set; }
    public string? Error      { get; set; }

    // ?? Platform identity (login card header) ?????????????????????????????????
    public string PlatformName { get; set; } = "SentinelLog";
    public string Organization { get; set; } = "NOC / SOC Platform";

    // ?? Stats bar (bottom of hero section) ???????????????????????????????????
    public decimal EpsNow               { get; set; }
    public int    ActiveSources         { get; set; }
    public long   TotalEventsToday      { get; set; }
    public double CompressionRatio      { get; set; }
    public double IngestionRateGbPerHour { get; set; }
}

// ?? Dashboard / KPI ???????????????????????????????????????????????????????????
public class DashboardViewModel
{
    public long   TotalEvents24h { get; set; }
    public int    CriticalAlerts { get; set; }
    public int    ErrorsPerHour  { get; set; }
    public int    Warnings       { get; set; }
    public int    ActiveSources  { get; set; }
    public int    OfflineSources { get; set; }
    public decimal EpsNow        { get; set; }
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RecentAlertRow> RecentAlerts { get; set; } = new();
    public List<TopSourceRow>   TopSources   { get; set; } = new();

    // Trend vs previous 24h window (null = no data)
    public double? TrendEvents   { get; set; }
    public double? TrendErrors   { get; set; }
    public double? TrendWarnings { get; set; }
}

public class RecentAlertRow
{
    public string Time     { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Source   { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string Message  { get; set; } = string.Empty;
    public string Status   { get; set; } = string.Empty;
}

public class TopSourceRow
{
    public string Source   { get; set; } = string.Empty;
    public string Ip       { get; set; } = string.Empty;
    public string Type     { get; set; } = string.Empty;
    public int    Events   { get; set; }
    public int    PctOfMax { get; set; }
    public string Color    { get; set; } = string.Empty;
}

// ?? Log Search ????????????????????????????????????????????????????????????????
public class LogSearchViewModel
{
    public string? Host       { get; set; }
    public string? Program    { get; set; }
    public string? SourceIp   { get; set; }
    public string? DateFrom   { get; set; }
    public string? DateTo     { get; set; }
    public string? Keyword    { get; set; }
    public string? Severity   { get; set; }
    public string? Facility   { get; set; }
    public string? DeviceType { get; set; }
    public int     Page       { get; set; } = 1;
    public int     PageSize   { get; set; } = 50;

    public string? SortOrder { get; set; } = "desc";
    public List<SystemLogRow> Results    { get; set; } = new();
    public int                TotalCount { get; set; }
    public long               QueryTimeMs { get; set; }
}

public class SystemLogRow
{
    public long    Id            { get; set; }
    public string  LogDatetime   { get; set; } = string.Empty;
    public string  ReceivedTime  { get; set; } = string.Empty;
    public string? Host          { get; set; }
    public string? Program       { get; set; }
    public string? Pid           { get; set; }
    public string? Message       { get; set; }
    public string? SourceIp      { get; set; }
    public string? SeverityLabel { get; set; }
    public string? FacilityLabel { get; set; }
}

// ?? Settings ??????????????????????????????????????????????????????????????????
public class PlatformSettingsViewModel
{
    public string  PlatformName          { get; set; } = string.Empty;
    public string? Organization          { get; set; }
    public string? ContactEmail          { get; set; }
    public string  Timezone              { get; set; } = "UTC";
    public string  DateFormat            { get; set; } = "YYYY-MM-DD HH:mm:ss";
    public bool    ShowMilliseconds       { get; set; } = true;
    public bool    AutoScrollFeed         { get; set; } = true;
    public bool    AudibleAlerts          { get; set; } = true;
    public int     FeedBufferSize         { get; set; } = 5000;
    public int     RefreshIntervalSeconds { get; set; } = 10;
}

public class StorageViewModel
{
    public double PrimaryStorageUsedTb   { get; set; }
    public double PrimaryStorageTotalTb  { get; set; }
    public double ArchiveStorageUsedTb   { get; set; }
    public double ArchiveStorageTotalTb  { get; set; }
    public double IngestionRateGbPerHour { get; set; }
    public double CompressionRatio       { get; set; }
    public List<StoragePolicyRow> Policies { get; set; } = new();
}

public class StoragePolicyRow
{
    public long    Id            { get; set; }
    public string? SourceType    { get; set; }
    public int?    RetentionDays { get; set; }
    public bool    Compression   { get; set; }
    public string  CreatedAt     { get; set; } = string.Empty;
}

// ?? Users management (Settings > Users) ?????????????????????????????????????
public class UsersManagementViewModel
{
    public List<UserDto> Users  { get; set; } = new();
    public string?       Error  { get; set; }
    public string?       Notice { get; set; }
}
