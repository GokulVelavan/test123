using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class NotificationChannelRepository
{
    private readonly SyslogDbContext _db;
    public NotificationChannelRepository(SyslogDbContext db) => _db = db;

    public async Task<List<NotificationChannelDto>> GetAllAsync() =>
        await _db.NotificationChannels.AsNoTracking()
            .OrderBy(n => n.Type)
            .Select(n => ToDto(n))
            .ToListAsync();

    public async Task<NotificationChannelDto?> GetByIdAsync(long id)
    {
        var n = await _db.NotificationChannels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return n is null ? null : ToDto(n);
    }

    public async Task<NotificationChannelDto> CreateAsync(CreateNotificationChannelRequest req)
    {
        var entity = new NotificationChannel
        {
            Name      = req.Name,
            Type      = req.Type,
            Target    = req.Target,
            Config    = req.Config,
            Enabled   = req.Enabled,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.NotificationChannels.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<NotificationChannelDto?> UpdateAsync(long id, UpdateNotificationChannelRequest req)
    {
        var entity = await _db.NotificationChannels.FindAsync(id);
        if (entity is null) return null;
        if (req.Name    is not null) entity.Name    = req.Name;
        if (req.Type    is not null) entity.Type    = req.Type;
        if (req.Target  is not null) entity.Target  = req.Target;
        if (req.Config  is not null) entity.Config  = req.Config;
        if (req.Enabled is not null) entity.Enabled = req.Enabled.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.NotificationChannels.FindAsync(id);
        if (entity is null) return false;
        entity.IsDeleted  = true;
        entity.UpdatedAt  = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<NotificationChannelDto?> ToggleEnabledAsync(long id, bool enabled)
    {
        var entity = await _db.NotificationChannels.FindAsync(id);
        if (entity is null) return null;
        entity.Enabled   = enabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    private static NotificationChannelDto ToDto(NotificationChannel n) => new()
    {
        Id        = n.Id,
        Name      = n.Name,
        Type      = n.Type,
        Target    = n.Target,
        Config    = n.Config,
        Enabled   = n.Enabled,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}
