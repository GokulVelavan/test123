using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Managers;

public interface IAnomalyManager
{
    Task<List<AnomalyRuleDto>>         GetRulesAsync();
    Task<AnomalyRuleDto?>              GetRuleByIdAsync(long id);
    Task<AnomalyRuleDto>               CreateRuleAsync(CreateAnomalyRuleRequest request);
    Task<AnomalyRuleDto?>              UpdateRuleAsync(long id, UpdateAnomalyRuleRequest request);
    Task<bool>                         DeleteRuleAsync(long id);
    Task<AnomalyRuleDto?>              ToggleRuleAsync(long id, bool enabled);

    Task<PagedResult<AnomalyEventDto>> GetEventsAsync(bool? acknowledged, int page, int pageSize);
    Task<AnomalyEventDto?>             AcknowledgeEventAsync(long id, string acknowledgedBy);
    Task<AnomalySummaryDto>            GetSummaryAsync();
}
