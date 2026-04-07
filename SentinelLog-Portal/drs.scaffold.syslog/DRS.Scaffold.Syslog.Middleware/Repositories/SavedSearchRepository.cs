using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class SavedSearchRepository : ISavedSearchRepository
{
    private readonly SyslogDbContext _db;
    public SavedSearchRepository(SyslogDbContext db) => _db = db;

    public async Task<List<SavedSearchDto>> GetAllAsync(long? userId)
    {
        var q = _db.SavedSearches.AsQueryable();
        if (userId.HasValue)
            q = q.Where(s => s.IsShared || s.UserId == userId.Value);

        return await q
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Select(s => Map(s))
            .ToListAsync();
    }

    public async Task<SavedSearchDto?> GetByIdAsync(long id)
    {
        var s = await _db.SavedSearches.FindAsync(id);
        return s is null || s.IsDeleted ? null : Map(s);
    }

    public async Task<SavedSearchDto> CreateAsync(CreateSavedSearchRequest req)
    {
        var s = new SavedSearch
        {
            Name        = req.Name,
            Description = req.Description,
            QueryJson   = req.QueryJson,
            UserId      = req.UserId,
            IsShared    = req.IsShared,
            CreatedAt   = DateTimeOffset.UtcNow
        };
        _db.SavedSearches.Add(s);
        await _db.SaveChangesAsync();
        return Map(s);
    }

    public async Task<SavedSearchDto?> UpdateAsync(long id, UpdateSavedSearchRequest req)
    {
        var s = await _db.SavedSearches.FindAsync(id);
        if (s is null || s.IsDeleted) return null;

        if (req.Name        != null) s.Name        = req.Name;
        if (req.Description != null) s.Description = req.Description;
        if (req.QueryJson   != null) s.QueryJson   = req.QueryJson;
        if (req.IsShared    != null) s.IsShared    = req.IsShared.Value;

        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Map(s);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var s = await _db.SavedSearches.FindAsync(id);
        if (s is null || s.IsDeleted) return false;
        s.IsDeleted = true;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private static SavedSearchDto Map(SavedSearch s) => new()
    {
        Id          = s.Id,
        Name        = s.Name,
        Description = s.Description,
        QueryJson   = s.QueryJson,
        UserId      = s.UserId,
        IsShared    = s.IsShared,
        CreatedAt   = s.CreatedAt,
        UpdatedAt   = s.UpdatedAt
    };
}
