using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class IncidentRepository : IIncidentRepository
{
    private readonly SyslogDbContext _db;
    public IncidentRepository(SyslogDbContext db) => _db = db;

    public async Task<PagedResult<IncidentDto>> GetAllAsync(string? status, string? priority, int page, int pageSize)
    {
        var q = _db.Incidents.Where(i => !i.IsDeleted).Include(i => i.Assignee).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(i => i.Status == status);
        if (!string.IsNullOrWhiteSpace(priority))
            q = q.Where(i => i.Priority == priority);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<IncidentDto> { Items = items.Select(Map).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IncidentDto?> GetByIdAsync(long id)
    {
        var i = await _db.Incidents.Include(x => x.Assignee).FirstOrDefaultAsync(x => x.Id == id);
        return i is null || i.IsDeleted ? null : Map(i);
    }

    public async Task<IncidentDto> CreateAsync(CreateIncidentRequest req)
    {
        var i = new Incident
        {
            Title         = req.Title,
            Description   = req.Description,
            Status        = req.Status,
            Priority      = req.Priority,
            AssigneeId    = req.AssigneeId,
            AlertEventId  = req.AlertEventId,
            Tags          = req.Tags,
            Notes         = req.Notes,
            AffectedHosts = req.AffectedHosts,
            CreatedAt     = DateTimeOffset.UtcNow
        };
        _db.Incidents.Add(i);
        await _db.SaveChangesAsync();
        return Map(i);
    }

    public async Task<IncidentDto?> UpdateAsync(long id, UpdateIncidentRequest req)
    {
        var i = await _db.Incidents.FindAsync(id);
        if (i is null || i.IsDeleted) return null;

        if (req.Title         != null) i.Title         = req.Title;
        if (req.Description   != null) i.Description   = req.Description;
        if (req.Status        != null) i.Status        = req.Status;
        if (req.Priority      != null) i.Priority      = req.Priority;
        if (req.AssigneeId    != null) i.AssigneeId    = req.AssigneeId;
        if (req.Tags          != null) i.Tags          = req.Tags;
        if (req.Notes         != null) i.Notes         = req.Notes;
        if (req.AffectedHosts != null) i.AffectedHosts = req.AffectedHosts;

        if (req.Status is "Resolved" or "Closed" && i.ResolvedAt is null)
            i.ResolvedAt = DateTimeOffset.UtcNow;

        i.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Map(i);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var i = await _db.Incidents.FindAsync(id);
        if (i is null || i.IsDeleted) return false;
        i.IsDeleted = true;
        i.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IncidentSummaryDto> GetSummaryAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var weekAgo = now.AddDays(-7);
        var all = await _db.Incidents.Where(i => !i.IsDeleted).ToListAsync();

        return new IncidentSummaryDto
        {
            TotalOpen     = all.Count(i => i.Status is "Open" or "Investigating" or "Contained"),
            TotalCritical = all.Count(i => i.Priority == "Critical"),
            TotalHigh     = all.Count(i => i.Priority == "High"),
            TotalResolved = all.Count(i => i.Status is "Resolved" or "Closed"),
            TotalThisWeek = all.Count(i => i.CreatedAt >= weekAgo)
        };
    }

    private static IncidentDto Map(Incident i) => new()
    {
        Id            = i.Id,
        Title         = i.Title,
        Description   = i.Description,
        Status        = i.Status,
        Priority      = i.Priority,
        AssigneeId    = i.AssigneeId,
        AssigneeName  = i.Assignee?.FullName ?? i.Assignee?.Username,
        AlertEventId  = i.AlertEventId,
        Tags          = i.Tags,
        Notes         = i.Notes,
        AffectedHosts = i.AffectedHosts,
        ResolvedAt    = i.ResolvedAt,
        CreatedAt     = i.CreatedAt,
        UpdatedAt     = i.UpdatedAt
    };
}
