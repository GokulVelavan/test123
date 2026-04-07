using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface IThreatIntelRepository
{
    Task<PagedResult<ThreatIndicatorDto>> GetAllAsync(string? type, bool? active, int page, int pageSize);
    Task<ThreatIndicatorDto?>             GetByIdAsync(long id);
    Task<ThreatIndicatorDto>              CreateAsync(CreateThreatIndicatorRequest request);
    Task<ThreatIndicatorDto?>             UpdateAsync(long id, UpdateThreatIndicatorRequest request);
    Task<bool>                            DeleteAsync(long id);
    Task<List<ThreatIndicatorDto>>        LookupAsync(string value);
    Task<ThreatIntelSummaryDto>           GetSummaryAsync();
}
