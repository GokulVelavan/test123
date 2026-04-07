using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class LogEntryManager : ILogEntryManager
{
    private readonly ILogEntryRepository _repo;
    public LogEntryManager(ILogEntryRepository repo) => _repo = repo;

    public Task<PagedResult<LogEventDto>> SearchLogsAsync(LogSearchRequest request)
        => _repo.SearchAsync(request);

    public Task<List<LogEventDto>> GetLiveFeedAsync(LiveLogQueryRequest request)
        => _repo.GetLiveAsync(request);

    public Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        => _repo.GetDashboardSummaryAsync();

    public Task<List<EventVolumeDto>> GetEventVolumeAsync(int hours, int bucketMinutes)
        => _repo.GetEventVolumeAsync(hours, bucketMinutes);

    public Task<SeverityBreakdownDto> GetSeverityBreakdownAsync(DateTimeOffset? from, DateTimeOffset? to)
        => _repo.GetSeverityBreakdownAsync(from, to);

    public Task<List<TopTalkerDto>> GetTopTalkersAsync(int topN, DateTimeOffset? from, DateTimeOffset? to)
        => _repo.GetTopTalkersAsync(topN, from, to);

    public Task<List<LogFacetDto>>        GetFacetsAsync(LogSearchRequest filter)
        => _repo.GetFacetsAsync(filter);

    public Task<List<HeatmapBucketDto>>   GetHeatmapAsync(int days)
        => _repo.GetHeatmapAsync(days);

    public Task<List<CategoryBreakdownDto>> GetCategoryBreakdownAsync(int days)
        => _repo.GetCategoryBreakdownAsync(days);
}
