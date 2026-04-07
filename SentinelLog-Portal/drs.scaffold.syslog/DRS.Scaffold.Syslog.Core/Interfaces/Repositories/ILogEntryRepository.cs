using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface ILogEntryRepository
{
    Task<PagedResult<LogEventDto>> SearchAsync(LogSearchRequest request);
    Task<List<LogEventDto>>        GetLiveAsync(LiveLogQueryRequest request);
    Task<DashboardSummaryDto>      GetDashboardSummaryAsync();
    Task<List<EventVolumeDto>>     GetEventVolumeAsync(int hours, int bucketMinutes);
    Task<SeverityBreakdownDto>     GetSeverityBreakdownAsync(DateTimeOffset? from, DateTimeOffset? to);
    Task<List<TopTalkerDto>>       GetTopTalkersAsync(int topN, DateTimeOffset? from, DateTimeOffset? to);
    // #41 facet sidebar
    Task<List<LogFacetDto>>        GetFacetsAsync(LogSearchRequest filter);
    // #51 activity heatmap
    Task<List<HeatmapBucketDto>>   GetHeatmapAsync(int days);
    // #52 security category breakdown
    Task<List<CategoryBreakdownDto>> GetCategoryBreakdownAsync(int days);
}
