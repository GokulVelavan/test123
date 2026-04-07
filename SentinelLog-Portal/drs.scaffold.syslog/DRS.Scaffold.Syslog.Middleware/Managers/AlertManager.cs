using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class AlertManager : IAlertManager
{
    private readonly IAlertRepository _repo;
    public AlertManager(IAlertRepository repo) => _repo = repo;

    public Task<List<AlertRuleDto>> GetRulesAsync()
        => _repo.GetAllRulesAsync();

    public Task<AlertRuleDto?> GetRuleByIdAsync(long id)
        => _repo.GetRuleByIdAsync(id);

    public Task<AlertRuleDto> CreateRuleAsync(CreateAlertRuleRequest request)
        => _repo.CreateRuleAsync(request);

    public Task<AlertRuleDto?> UpdateRuleAsync(long id, UpdateAlertRuleRequest request)
        => _repo.UpdateRuleAsync(id, request);

    public Task<bool> DeleteRuleAsync(long id)
        => _repo.DeleteRuleAsync(id);

    public Task<AlertRuleDto?> ToggleRuleAsync(long id, bool enabled)
        => _repo.ToggleRuleAsync(id, enabled);

    public Task<PagedResult<AlertEventDto>> GetEventsAsync(string? severity, bool? acknowledged, int page, int pageSize)
        => _repo.GetEventsAsync(severity, acknowledged, page, pageSize);

    public Task<AlertEventDto?> GetEventByIdAsync(long id)
        => _repo.GetEventByIdAsync(id);

    public Task<AlertEventDto?> AcknowledgeEventAsync(long id, AcknowledgeAlertEventRequest request)
        => _repo.AcknowledgeEventAsync(id, request);

    public Task<AlertSummaryDto> GetSummaryAsync()
        => _repo.GetSummaryAsync();
}
