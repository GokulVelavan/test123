using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class ThreatIntelManager : IThreatIntelManager
{
    private readonly IThreatIntelRepository _repo;
    public ThreatIntelManager(IThreatIntelRepository repo) => _repo = repo;

    public Task<PagedResult<ThreatIndicatorDto>> GetAllAsync(string? type, bool? active, int page, int pageSize)
        => _repo.GetAllAsync(type, active, page, pageSize);

    public Task<ThreatIndicatorDto?> GetByIdAsync(long id)
        => _repo.GetByIdAsync(id);

    public Task<ThreatIndicatorDto> CreateAsync(CreateThreatIndicatorRequest request)
        => _repo.CreateAsync(request);

    public Task<ThreatIndicatorDto?> UpdateAsync(long id, UpdateThreatIndicatorRequest request)
        => _repo.UpdateAsync(id, request);

    public Task<bool> DeleteAsync(long id)
        => _repo.DeleteAsync(id);

    public async Task<ThreatCheckResultDto> CheckValueAsync(string value)
    {
        var matches = await _repo.LookupAsync(value);
        return new ThreatCheckResultDto
        {
            Value     = value,
            IsMatched = matches.Count > 0,
            Matches   = matches
        };
    }

    public Task<ThreatIntelSummaryDto> GetSummaryAsync()
        => _repo.GetSummaryAsync();
}
