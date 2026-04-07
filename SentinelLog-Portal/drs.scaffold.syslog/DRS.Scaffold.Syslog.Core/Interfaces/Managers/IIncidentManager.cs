using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Managers;

public interface IIncidentManager
{
    Task<PagedResult<IncidentDto>> GetAllAsync(string? status, string? priority, int page, int pageSize);
    Task<IncidentDto?>             GetByIdAsync(long id);
    Task<IncidentDto>              CreateAsync(CreateIncidentRequest request);
    Task<IncidentDto?>             UpdateAsync(long id, UpdateIncidentRequest request);
    Task<bool>                     DeleteAsync(long id);
    Task<IncidentSummaryDto>       GetSummaryAsync();
}
