using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Managers;

public interface ILogSourceManager
{
    Task<PagedResult<LogSourceDto>> GetSourcesAsync(string? search, string? status, string? deviceType, int page, int pageSize);
    Task<LogSourceDto?>             GetSourceByIdAsync(long id);
    Task<LogSourceDto>              AddSourceAsync(CreateLogSourceRequest request);
    Task<LogSourceDto?>             UpdateSourceAsync(long id, UpdateLogSourceRequest request);
    Task<bool>                      RemoveSourceAsync(long id);
    Task<LogSourceSummaryDto>       GetSummaryAsync();
    // #72
    Task<int>                       BulkDeleteAsync(List<long> ids);
    Task<int>                       BulkSetStatusAsync(List<long> ids, string status);
    // #18/#22/#23
    Task<List<SourceActivityDto>>   GetActivityAsync(long sourceId, int hours);
}
