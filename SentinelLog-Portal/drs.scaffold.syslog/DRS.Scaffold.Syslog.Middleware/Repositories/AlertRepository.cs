using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class AlertRepository : IAlertRepository
{
    private readonly SyslogDbContext _db;
    public AlertRepository(SyslogDbContext db) => _db = db;

    // ?? Rules ??????????????????????????????????????????????????????????????????

    public async Task<List<AlertRuleDto>> GetAllRulesAsync() =>
        await _db.AlertRules.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => ToRuleDto(r))
            .ToListAsync();

    public async Task<AlertRuleDto?> GetRuleByIdAsync(long id)
    {
        var r = await _db.AlertRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return r is null ? null : ToRuleDto(r);
    }

    public async Task<AlertRuleDto> CreateRuleAsync(CreateAlertRuleRequest req)
    {
        var entity = new AlertRule
        {
            Name                = req.Name,
            Description         = req.Description,
            Severity            = req.Severity,
            ConditionExpression = req.ConditionExpression,
            SourceType          = req.SourceType,
            TimeWindow          = req.TimeWindow,
            Enabled             = req.Enabled,
            CreatedAt           = DateTimeOffset.UtcNow,
            UpdatedAt           = DateTimeOffset.UtcNow
        };
        _db.AlertRules.Add(entity);
        await _db.SaveChangesAsync();
        return ToRuleDto(entity);
    }

    public async Task<AlertRuleDto?> UpdateRuleAsync(long id, UpdateAlertRuleRequest req)
    {
        var entity = await _db.AlertRules.FindAsync(id);
        if (entity is null) return null;

        if (req.Name                is not null) entity.Name                = req.Name;
        if (req.Description         is not null) entity.Description         = req.Description;
        if (req.Severity            is not null) entity.Severity            = req.Severity;
        if (req.ConditionExpression is not null) entity.ConditionExpression = req.ConditionExpression;
        if (req.SourceType          is not null) entity.SourceType          = req.SourceType;
        if (req.TimeWindow          is not null) entity.TimeWindow          = req.TimeWindow;
        if (req.Enabled             is not null) entity.Enabled             = req.Enabled.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return ToRuleDto(entity);
    }

    public async Task<bool> DeleteRuleAsync(long id)
    {
        var entity = await _db.AlertRules.FindAsync(id);
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<AlertRuleDto?> ToggleRuleAsync(long id, bool enabled)
    {
        var entity = await _db.AlertRules.FindAsync(id);
        if (entity is null) return null;
        entity.Enabled   = enabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return ToRuleDto(entity);
    }

    // ?? Events ?????????????????????????????????????????????????????????????????

    public async Task<PagedResult<AlertEventDto>> GetEventsAsync(
        string? severity, bool? acknowledged, int page, int pageSize)
    {
        var q = _db.AlertEvents
            .Where(e => !e.IsDeleted)
            .Include(e => e.Rule)
            .Include(e => e.LogEvent)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var sevLower = severity.ToLowerInvariant();
            q = q.Where(e => e.Severity != null && e.Severity.ToLower() == sevLower);
        }

        if (acknowledged.HasValue)
            q = q.Where(e => e.Acknowledged == acknowledged.Value);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(e => e.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => ToEventDto(e))
            .ToListAsync();

        return new PagedResult<AlertEventDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<AlertEventDto?> GetEventByIdAsync(long id)
    {
        var e = await _db.AlertEvents.AsNoTracking()
            .Include(x => x.Rule)
            .Include(x => x.LogEvent)
            .FirstOrDefaultAsync(x => x.Id == id);
        return e is null ? null : ToEventDto(e);
    }

    public async Task<AlertEventDto?> AcknowledgeEventAsync(long id, AcknowledgeAlertEventRequest req)
    {
        var entity = await _db.AlertEvents
            .Include(x => x.Rule)
            .Include(x => x.LogEvent)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return null;
        entity.Acknowledged   = true;
        entity.AcknowledgedBy = req.AcknowledgedBy;
        entity.AcknowledgedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt      = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return ToEventDto(entity);
    }

    public async Task<AlertSummaryDto> GetSummaryAsync()
    {
        var events = await _db.AlertEvents.Where(e => !e.IsDeleted).ToListAsync();

        return new AlertSummaryDto
        {
            TotalFiredToday      = events.Count,
            CriticalCount        = events.Count(e => e.Severity == "Critical"),
            ErrorCount           = events.Count(e => e.Severity == "Error"),
            WarningCount         = events.Count(e => e.Severity == "Warning"),
            NoticeCount          = events.Count(e => e.Severity == "Notice"),
            InfoCount            = events.Count(e => e.Severity == "Info"),
            AcknowledgedCount    = events.Count(e => e.Acknowledged),
            UnacknowledgedCount  = events.Count(e => !e.Acknowledged)
        };
    }

    // ?? Mappers ????????????????????????????????????????????????????????????????

    private static AlertRuleDto ToRuleDto(AlertRule r) => new()
    {
        Id                  = r.Id,
        Name                = r.Name,
        Description         = r.Description,
        Severity            = r.Severity,
        ConditionExpression = r.ConditionExpression,
        SourceType          = r.SourceType,
        TimeWindow          = r.TimeWindow,
        Enabled             = r.Enabled,
        CreatedAt           = r.CreatedAt,
        UpdatedAt           = r.UpdatedAt
    };

    private static AlertEventDto ToEventDto(AlertEvent e) => new()
    {
        Id             = e.Id,
        RuleId         = e.RuleId,
        RuleName       = e.Rule?.Name,
        LogEventId     = e.LogEventId,
        SourceHost     = e.LogEvent?.Hostname,
        Severity       = e.Severity,
        Message        = e.Message,
        Acknowledged   = e.Acknowledged,
        AcknowledgedBy = e.AcknowledgedBy,
        AcknowledgedAt = e.AcknowledgedAt,
        TriggeredAt    = e.TriggeredAt,
        CreatedAt      = e.CreatedAt
    };
}
