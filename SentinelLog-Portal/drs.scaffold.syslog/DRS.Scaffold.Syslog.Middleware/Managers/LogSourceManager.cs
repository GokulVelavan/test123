using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class LogSourceManager : ILogSourceManager
{
    private readonly ILogSourceRepository _repo;
    public LogSourceManager(ILogSourceRepository repo) => _repo = repo;

    public Task<PagedResult<LogSourceDto>> GetSourcesAsync(
        string? search, string? status, string? deviceType, int page, int pageSize)
        => _repo.GetAllAsync(search, status, deviceType, page, pageSize);

    public Task<LogSourceDto?> GetSourceByIdAsync(long id)
        => _repo.GetByIdAsync(id);

    public Task<LogSourceDto> AddSourceAsync(CreateLogSourceRequest request)
        => _repo.CreateAsync(request);

    public Task<LogSourceDto?> UpdateSourceAsync(long id, UpdateLogSourceRequest request)
        => _repo.UpdateAsync(id, request);

    public async Task<bool> RemoveSourceAsync(long id)
    {
        if (!await _repo.ExistsAsync(id)) return false;
        return await _repo.DeleteAsync(id);
    }

    public Task<LogSourceSummaryDto> GetSummaryAsync()
        => _repo.GetSummaryAsync();

    public Task<int> BulkDeleteAsync(List<long> ids)
        => _repo.BulkDeleteAsync(ids);

    public Task<int> BulkSetStatusAsync(List<long> ids, string status)
        => _repo.BulkSetStatusAsync(ids, status);

    public Task<List<SourceActivityDto>> GetActivityAsync(long sourceId, int hours)
        => _repo.GetActivityAsync(sourceId, hours);
}
