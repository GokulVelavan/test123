using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface IAlertRepository
{
    // Rules
    Task<List<AlertRuleDto>>  GetAllRulesAsync();
    Task<AlertRuleDto?>       GetRuleByIdAsync(long id);
    Task<AlertRuleDto>        CreateRuleAsync(CreateAlertRuleRequest request);
    Task<AlertRuleDto?>       UpdateRuleAsync(long id, UpdateAlertRuleRequest request);
    Task<bool>                DeleteRuleAsync(long id);
    Task<AlertRuleDto?>       ToggleRuleAsync(long id, bool enabled);

    // Events
    Task<PagedResult<AlertEventDto>> GetEventsAsync(string? severity, bool? acknowledged, int page, int pageSize);
    Task<AlertEventDto?>             GetEventByIdAsync(long id);
    Task<AlertEventDto?>             AcknowledgeEventAsync(long id, AcknowledgeAlertEventRequest request);
    Task<AlertSummaryDto>            GetSummaryAsync();
}
