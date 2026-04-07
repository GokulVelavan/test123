using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Managers;

public interface ILogEntryManager
{
    Task<PagedResult<LogEventDto>> SearchLogsAsync(LogSearchRequest request);
    Task<List<LogEventDto>>        GetLiveFeedAsync(LiveLogQueryRequest request);
    Task<DashboardSummaryDto>      GetDashboardSummaryAsync();
    Task<List<EventVolumeDto>>     GetEventVolumeAsync(int hours, int bucketMinutes);
    Task<SeverityBreakdownDto>     GetSeverityBreakdownAsync(DateTimeOffset? from, DateTimeOffset? to);
    Task<List<TopTalkerDto>>       GetTopTalkersAsync(int topN, DateTimeOffset? from, DateTimeOffset? to);
    Task<List<LogFacetDto>>        GetFacetsAsync(LogSearchRequest filter);
    Task<List<HeatmapBucketDto>>   GetHeatmapAsync(int days);
    Task<List<CategoryBreakdownDto>> GetCategoryBreakdownAsync(int days);
}
