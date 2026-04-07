using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class IncidentManager : IIncidentManager
{
    private readonly IIncidentRepository _repo;
    public IncidentManager(IIncidentRepository repo) => _repo = repo;

    public Task<PagedResult<IncidentDto>> GetAllAsync(string? status, string? priority, int page, int pageSize)
        => _repo.GetAllAsync(status, priority, page, pageSize);

    public Task<IncidentDto?> GetByIdAsync(long id)
        => _repo.GetByIdAsync(id);

    public Task<IncidentDto> CreateAsync(CreateIncidentRequest request)
        => _repo.CreateAsync(request);

    public Task<IncidentDto?> UpdateAsync(long id, UpdateIncidentRequest request)
        => _repo.UpdateAsync(id, request);

    public Task<bool> DeleteAsync(long id)
        => _repo.DeleteAsync(id);

    public Task<IncidentSummaryDto> GetSummaryAsync()
        => _repo.GetSummaryAsync();
}
