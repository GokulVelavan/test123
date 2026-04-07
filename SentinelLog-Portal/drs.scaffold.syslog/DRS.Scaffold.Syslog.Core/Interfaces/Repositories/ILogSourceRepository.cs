using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface ILogSourceRepository
{
    Task<PagedResult<LogSourceDto>> GetAllAsync(string? search, string? status, string? deviceType, int page, int pageSize);
    Task<LogSourceDto?>             GetByIdAsync(long id);
    Task<LogSourceDto>              CreateAsync(CreateLogSourceRequest request);
    Task<LogSourceDto?>             UpdateAsync(long id, UpdateLogSourceRequest request);
    Task<bool>                      DeleteAsync(long id);
    Task<LogSourceSummaryDto>       GetSummaryAsync();
    Task<bool>                      ExistsAsync(long id);
    // #72 — bulk operations
    Task<int>                       BulkDeleteAsync(List<long> ids);
    Task<int>                       BulkSetStatusAsync(List<long> ids, string status);
    // #18/#22/#23 — per-source activity time-series
    Task<List<SourceActivityDto>>   GetActivityAsync(long sourceId, int hours);
}
