using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class LogSourceRepository : ILogSourceRepository
{
    private readonly SyslogDbContext _db;
    public LogSourceRepository(SyslogDbContext db) => _db = db;

    public async Task<PagedResult<LogSourceDto>> GetAllAsync(
        string? search, string? status, string? deviceType, int page, int pageSize)
    {
        var q = _db.Sources.AsNoTracking().Where(s => !s.IsDeleted).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(s =>
                (s.Name     != null && s.Name.Contains(search)) ||
                (s.IpAddress != null && s.IpAddress.Contains(search)) ||
                (s.Location != null && s.Location.Contains(search)));

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(s => s.Status == status);

        if (!string.IsNullOrWhiteSpace(deviceType))
            q = q.Where(s => s.DeviceType == deviceType);

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToDto(s))
            .ToListAsync();

        // #73 — compute CurrentEps: events per source in the last 60 seconds / 60
        if (items.Count > 0)
        {
            var cutoff   = DateTimeOffset.UtcNow.AddSeconds(-60);
            var sourceIds = items.Select(i => i.Id).ToList();
            var epsCounts = await _db.LogEvents
                .AsNoTracking()
                .Where(l => l.SourceId != null && sourceIds.Contains(l.SourceId.Value) && l.ReceivedTime >= cutoff)
                .GroupBy(l => l.SourceId!.Value)
                .Select(g => new { SourceId = g.Key, Count = g.Count() })
                .ToListAsync();

            var epsMap = epsCounts.ToDictionary(x => x.SourceId, x => x.Count / 60);
            foreach (var item in items)
                if (epsMap.TryGetValue(item.Id, out var eps))
                    item.CurrentEps = eps;
        }

        return new PagedResult<LogSourceDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<LogSourceDto?> GetByIdAsync(long id)
    {
        var s = await _db.Sources.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return s is null ? null : ToDto(s);
    }

    public async Task<LogSourceDto> CreateAsync(CreateLogSourceRequest req)
    {
        var entity = new LogSource
        {
            Name       = req.Name,
            IpAddress  = req.IpAddress,
            DeviceType = req.DeviceType,
            OsType     = req.OsType,
            Location   = req.Location,
            Status     = req.Status,
            CreatedAt  = DateTimeOffset.UtcNow,
            CreatedBy  = req.CreatedBy,
            IsDeleted  = false
        };
        _db.Sources.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<LogSourceDto?> UpdateAsync(long id, UpdateLogSourceRequest req)
    {
        var entity = await _db.Sources.FindAsync(id);
        if (entity is null) return null;

        if (req.Name       is not null) entity.Name       = req.Name;
        if (req.IpAddress  is not null) entity.IpAddress  = req.IpAddress;
        if (req.DeviceType is not null) entity.DeviceType = req.DeviceType;
        if (req.OsType     is not null) entity.OsType     = req.OsType;
        if (req.Location   is not null) entity.Location   = req.Location;
        if (req.Status     is not null) entity.Status     = req.Status;
        entity.UpdatedAt  = DateTimeOffset.UtcNow;
        entity.UpdatedBy  = req.UpdatedBy;

        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.Sources.FindAsync(id);
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<LogSourceSummaryDto> GetSummaryAsync()
    {
        var counts = await _db.Sources
            .Where(s => !s.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new LogSourceSummaryDto
            {
                TotalSources  = g.Count(),
                OnlineCount   = g.Count(s => s.Status == "Online"),
                OfflineCount  = g.Count(s => s.Status == "Offline"),
                DegradedCount = g.Count(s => s.Status == "Degraded"),
                PendingCount  = g.Count(s => s.Status == "Pending")
            })
            .FirstOrDefaultAsync();

        return counts ?? new LogSourceSummaryDto();
    }

    public Task<bool> ExistsAsync(long id) =>
        _db.Sources.AnyAsync(s => s.Id == id && !s.IsDeleted);

    // #72 — bulk operations
    public async Task<int> BulkDeleteAsync(List<long> ids)
    {
        var entities = await _db.Sources.Where(s => ids.Contains(s.Id)).ToListAsync();
        foreach (var e in entities) { e.IsDeleted = true; e.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync();
        return entities.Count;
    }

    public async Task<int> BulkSetStatusAsync(List<long> ids, string status)
    {
        var entities = await _db.Sources.Where(s => ids.Contains(s.Id)).ToListAsync();
        foreach (var e in entities) { e.Status = status; e.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync();
        return entities.Count;
    }

    // #18/#22/#23 — per-source activity time-series
    public async Task<List<SourceActivityDto>> GetActivityAsync(long sourceId, int hours)
    {
        var from = DateTimeOffset.UtcNow.AddHours(-hours);
        var entries = await _db.LogEvents
            .AsNoTracking()
            .Where(l => l.SourceId == sourceId && l.EventTime >= from)
            .Select(l => l.EventTime)
            .ToListAsync();

        var bucketMinutes = hours <= 6 ? 15 : hours <= 24 ? 60 : 360;

        return entries
            .GroupBy(t =>
            {
                var slot = (long)(t - from).TotalMinutes / bucketMinutes;
                return from.AddMinutes(slot * bucketMinutes);
            })
            .Select(g => new SourceActivityDto
            {
                BucketTime = g.Key,
                EventCount = g.Count()
            })
            .OrderBy(b => b.BucketTime)
            .ToList();
    }

    private static LogSourceDto ToDto(LogSource s) => new()
    {
        Id         = s.Id,
        Name       = s.Name,
        IpAddress  = s.IpAddress,
        DeviceType = s.DeviceType,
        OsType     = s.OsType,
        Location   = s.Location,
        Status     = s.Status,
        LastSeen   = s.LastSeen,
        CreatedAt  = s.CreatedAt,
        CreatedBy  = s.CreatedBy,
        UpdatedAt  = s.UpdatedAt,
        UpdatedBy  = s.UpdatedBy
    };
}
