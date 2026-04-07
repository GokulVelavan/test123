using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class AnomalyManager : IAnomalyManager
{
    private readonly SyslogDbContext _db;
    public AnomalyManager(SyslogDbContext db) => _db = db;

    // ── Rules ──────────────────────────────────────────────────────────────────

    public async Task<List<AnomalyRuleDto>> GetRulesAsync()
        => await _db.AnomalyRules
            .OrderBy(r => r.Name)
            .Select(r => MapRule(r))
            .ToListAsync();

    public async Task<AnomalyRuleDto?> GetRuleByIdAsync(long id)
    {
        var r = await _db.AnomalyRules.FindAsync(id);
        return r is null || r.IsDeleted ? null : MapRule(r);
    }

    public async Task<AnomalyRuleDto> CreateRuleAsync(CreateAnomalyRuleRequest req)
    {
        var r = new AnomalyRule
        {
            Name                  = req.Name,
            Description           = req.Description,
            MetricType            = req.MetricType,
            BaselineWindowMinutes = req.BaselineWindowMinutes,
            ThresholdMultiplier   = req.ThresholdMultiplier,
            AbsoluteMinimum       = req.AbsoluteMinimum,
            Severity              = req.Severity,
            AutoCreateIncident    = req.AutoCreateIncident,
            Enabled               = req.Enabled,
            CreatedAt             = DateTimeOffset.UtcNow
        };
        _db.AnomalyRules.Add(r);
        await _db.SaveChangesAsync();
        return MapRule(r);
    }

    public async Task<AnomalyRuleDto?> UpdateRuleAsync(long id, UpdateAnomalyRuleRequest req)
    {
        var r = await _db.AnomalyRules.FindAsync(id);
        if (r is null || r.IsDeleted) return null;

        if (req.Name                  != null) r.Name                  = req.Name;
        if (req.Description           != null) r.Description           = req.Description;
        if (req.MetricType            != null) r.MetricType            = req.MetricType;
        if (req.BaselineWindowMinutes != null) r.BaselineWindowMinutes = req.BaselineWindowMinutes.Value;
        if (req.ThresholdMultiplier   != null) r.ThresholdMultiplier   = req.ThresholdMultiplier.Value;
        if (req.AbsoluteMinimum       != null) r.AbsoluteMinimum       = req.AbsoluteMinimum.Value;
        if (req.Severity              != null) r.Severity              = req.Severity;
        if (req.AutoCreateIncident    != null) r.AutoCreateIncident    = req.AutoCreateIncident.Value;
        if (req.Enabled               != null) r.Enabled               = req.Enabled.Value;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return MapRule(r);
    }

    public async Task<bool> DeleteRuleAsync(long id)
    {
        var r = await _db.AnomalyRules.FindAsync(id);
        if (r is null || r.IsDeleted) return false;
        r.IsDeleted = true; r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<AnomalyRuleDto?> ToggleRuleAsync(long id, bool enabled)
    {
        var r = await _db.AnomalyRules.FindAsync(id);
        if (r is null || r.IsDeleted) return null;
        r.Enabled = enabled; r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return MapRule(r);
    }

    // ── Events ─────────────────────────────────────────────────────────────────

    public async Task<PagedResult<AnomalyEventDto>> GetEventsAsync(bool? acknowledged, int page, int pageSize)
    {
        var q = _db.AnomalyEvents.Include(e => e.Rule).AsQueryable();
        if (acknowledged.HasValue) q = q.Where(e => e.Acknowledged == acknowledged.Value);
        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(e => e.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => MapEvent(e))
            .ToListAsync();
        return new PagedResult<AnomalyEventDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<AnomalyEventDto?> AcknowledgeEventAsync(long id, string acknowledgedBy)
    {
        var e = await _db.AnomalyEvents.Include(x => x.Rule).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null || e.IsDeleted) return null;
        e.Acknowledged    = true;
        e.AcknowledgedBy  = acknowledgedBy;
        e.AcknowledgedAt  = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return MapEvent(e);
    }

    public async Task<AnomalySummaryDto> GetSummaryAsync()
    {
        var today = DateTimeOffset.UtcNow.Date;
        var rules  = await _db.AnomalyRules.ToListAsync();
        var events = await _db.AnomalyEvents.ToListAsync();
        return new AnomalySummaryDto
        {
            TotalRules           = rules.Count,
            EnabledRules         = rules.Count(r => r.Enabled),
            EventsToday          = events.Count(e => e.DetectedAt.Date == today),
            UnacknowledgedEvents = events.Count(e => !e.Acknowledged)
        };
    }

    private static AnomalyRuleDto MapRule(AnomalyRule r) => new()
    {
        Id                    = r.Id,
        Name                  = r.Name,
        Description           = r.Description,
        MetricType            = r.MetricType,
        BaselineWindowMinutes = r.BaselineWindowMinutes,
        ThresholdMultiplier   = r.ThresholdMultiplier,
        AbsoluteMinimum       = r.AbsoluteMinimum,
        Severity              = r.Severity,
        AutoCreateIncident    = r.AutoCreateIncident,
        Enabled               = r.Enabled,
        LastFiredAt           = r.LastFiredAt,
        FireCount             = r.FireCount,
        CreatedAt             = r.CreatedAt
    };

    private static AnomalyEventDto MapEvent(AnomalyEvent e) => new()
    {
        Id              = e.Id,
        RuleId          = e.RuleId,
        RuleName        = e.Rule?.Name,
        AffectedEntity  = e.AffectedEntity,
        BaselineValue   = e.BaselineValue,
        ObservedValue   = e.ObservedValue,
        DeviationRatio  = e.DeviationRatio,
        Details         = e.Details,
        Acknowledged    = e.Acknowledged,
        AcknowledgedBy  = e.AcknowledgedBy,
        AcknowledgedAt  = e.AcknowledgedAt,
        DetectedAt      = e.DetectedAt,
        Severity        = e.Rule?.Severity ?? "High"
    };
}
