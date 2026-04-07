using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class SourceGroupRepository : ISourceGroupRepository
{
    private readonly SyslogDbContext _db;
    public SourceGroupRepository(SyslogDbContext db) => _db = db;

    private static SourceGroupDto ToDto(SourceGroup g) => new(
        g.Id,
        g.Name,
        g.Description,
        g.Color,
        g.Members.Select(m => m.SourceId).ToList(),
        g.CreatedAt);

    public async Task<List<SourceGroupDto>> GetAllAsync()
        => await _db.SourceGroups
            .Include(g => g.Members)
            .OrderBy(g => g.Name)
            .Select(g => ToDto(g))
            .ToListAsync();

    public async Task<SourceGroupDto?> GetByIdAsync(long id)
    {
        var g = await _db.SourceGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);
        return g is null ? null : ToDto(g);
    }

    public async Task<SourceGroupDto> CreateAsync(CreateSourceGroupRequest request)
    {
        var g = new SourceGroup
        {
            Name        = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Color       = request.Color,
            CreatedAt   = DateTimeOffset.UtcNow,
        };
        _db.SourceGroups.Add(g);
        await _db.SaveChangesAsync();
        g.Members = new List<SourceGroupMember>();
        return ToDto(g);
    }

    public async Task<SourceGroupDto?> UpdateAsync(long id, UpdateSourceGroupRequest request)
    {
        var g = await _db.SourceGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id);
        if (g is null) return null;
        g.Name        = request.Name.Trim();
        g.Description = request.Description?.Trim();
        g.Color       = request.Color;
        await _db.SaveChangesAsync();
        return ToDto(g);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var g = await _db.SourceGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (g is null) return false;
        g.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateMembersAsync(long groupId, List<long> sourceIds)
    {
        var g = await _db.SourceGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (g is null) return false;

        // Remove existing members
        var existing = await _db.SourceGroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
        _db.SourceGroupMembers.RemoveRange(existing);

        // Add new members
        foreach (var sid in sourceIds.Distinct())
            _db.SourceGroupMembers.Add(new SourceGroupMember { GroupId = groupId, SourceId = sid });

        await _db.SaveChangesAsync();
        return true;
    }
}
