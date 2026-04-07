using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class ThreatIntelRepository : IThreatIntelRepository
{
    private readonly SyslogDbContext _db;
    public ThreatIntelRepository(SyslogDbContext db) => _db = db;

    public async Task<PagedResult<ThreatIndicatorDto>> GetAllAsync(string? type, bool? active, int page, int pageSize)
    {
        var q = _db.ThreatIndicators.AsQueryable();
        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(t => t.Type == type);
        if (active.HasValue)
            q = q.Where(t => t.IsActive == active.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => Map(t))
            .ToListAsync();

        return new PagedResult<ThreatIndicatorDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<ThreatIndicatorDto?> GetByIdAsync(long id)
    {
        var t = await _db.ThreatIndicators.FindAsync(id);
        return t is null || t.IsDeleted ? null : Map(t);
    }

    public async Task<ThreatIndicatorDto> CreateAsync(CreateThreatIndicatorRequest req)
    {
        var t = new ThreatIndicator
        {
            Type           = req.Type,
            Value          = req.Value.Trim(),
            ThreatCategory = req.ThreatCategory,
            Confidence     = req.Confidence,
            Source         = req.Source,
            Description    = req.Description,
            IsActive       = req.IsActive,
            ExpiresAt      = req.ExpiresAt,
            CreatedAt      = DateTimeOffset.UtcNow
        };
        _db.ThreatIndicators.Add(t);
        await _db.SaveChangesAsync();
        return Map(t);
    }

    public async Task<ThreatIndicatorDto?> UpdateAsync(long id, UpdateThreatIndicatorRequest req)
    {
        var t = await _db.ThreatIndicators.FindAsync(id);
        if (t is null || t.IsDeleted) return null;

        if (req.ThreatCategory != null) t.ThreatCategory = req.ThreatCategory;
        if (req.Confidence     != null) t.Confidence     = req.Confidence.Value;
        if (req.Source         != null) t.Source         = req.Source;
        if (req.Description    != null) t.Description    = req.Description;
        if (req.IsActive       != null) t.IsActive       = req.IsActive.Value;
        if (req.ExpiresAt      != null) t.ExpiresAt      = req.ExpiresAt;

        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Map(t);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var t = await _db.ThreatIndicators.FindAsync(id);
        if (t is null || t.IsDeleted) return false;
        t.IsDeleted = true;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ThreatIndicatorDto>> LookupAsync(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return await _db.ThreatIndicators
            .Where(t => t.IsActive && t.Value.ToLower() == v)
            .Select(t => Map(t))
            .ToListAsync();
    }

    public async Task<ThreatIntelSummaryDto> GetSummaryAsync()
    {
        var q = _db.ThreatIndicators;
        return new ThreatIntelSummaryDto
        {
            TotalIndicators  = await q.CountAsync(),
            ActiveIndicators = await q.CountAsync(t => t.IsActive),
            IpIndicators     = await q.CountAsync(t => t.Type == "IP"),
            DomainIndicators = await q.CountAsync(t => t.Type == "Domain"),
            HashIndicators   = await q.CountAsync(t => t.Type == "Hash")
        };
    }

    private static ThreatIndicatorDto Map(ThreatIndicator t) => new()
    {
        Id             = t.Id,
        Type           = t.Type,
        Value          = t.Value,
        ThreatCategory = t.ThreatCategory,
        Confidence     = t.Confidence,
        Source         = t.Source,
        Description    = t.Description,
        Origin         = t.Origin,
        IsActive       = t.IsActive,
        ExpiresAt      = t.ExpiresAt,
        CreatedAt      = t.CreatedAt
    };
}
