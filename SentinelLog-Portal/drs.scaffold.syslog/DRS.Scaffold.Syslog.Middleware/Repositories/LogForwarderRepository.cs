using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class LogForwarderRepository : ILogForwarderRepository
{
    private readonly SyslogDbContext _db;
    public LogForwarderRepository(SyslogDbContext db) => _db = db;

    public async Task<List<LogForwarderDto>> GetAllAsync()
        => await _db.LogForwarders
            .OrderBy(f => f.Name)
            .Select(f => Map(f))
            .ToListAsync();

    public async Task<LogForwarderDto?> GetByIdAsync(long id)
    {
        var f = await _db.LogForwarders.FindAsync(id);
        return f is null || f.IsDeleted ? null : Map(f);
    }

    public async Task<LogForwarderDto> CreateAsync(CreateLogForwarderRequest req)
    {
        var f = new LogForwarder
        {
            Name           = req.Name,
            Protocol       = req.Protocol,
            Host           = req.Host,
            Port           = req.Port,
            Format         = req.Format,
            SeverityFilter = req.SeverityFilter,
            SourceFilter   = req.SourceFilter,
            Enabled        = req.Enabled,
            TlsCertPath    = req.TlsCertPath,
            Description    = req.Description,
            CreatedAt      = DateTimeOffset.UtcNow
        };
        _db.LogForwarders.Add(f);
        await _db.SaveChangesAsync();
        return Map(f);
    }

    public async Task<LogForwarderDto?> UpdateAsync(long id, UpdateLogForwarderRequest req)
    {
        var f = await _db.LogForwarders.FindAsync(id);
        if (f is null || f.IsDeleted) return null;

        if (req.Name           != null) f.Name           = req.Name;
        if (req.Protocol       != null) f.Protocol       = req.Protocol;
        if (req.Host           != null) f.Host           = req.Host;
        if (req.Port           != null) f.Port           = req.Port.Value;
        if (req.Format         != null) f.Format         = req.Format;
        if (req.SeverityFilter != null) f.SeverityFilter = req.SeverityFilter;
        if (req.SourceFilter   != null) f.SourceFilter   = req.SourceFilter;
        if (req.Enabled        != null) f.Enabled        = req.Enabled.Value;
        if (req.TlsCertPath    != null) f.TlsCertPath    = req.TlsCertPath;
        if (req.Description    != null) f.Description    = req.Description;

        f.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Map(f);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var f = await _db.LogForwarders.FindAsync(id);
        if (f is null || f.IsDeleted) return false;
        f.IsDeleted = true;
        f.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<LogForwarderDto?> ToggleAsync(long id, bool enabled)
    {
        var f = await _db.LogForwarders.FindAsync(id);
        if (f is null || f.IsDeleted) return null;
        f.Enabled   = enabled;
        f.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Map(f);
    }

    private static LogForwarderDto Map(LogForwarder f) => new()
    {
        Id               = f.Id,
        Name             = f.Name,
        Protocol         = f.Protocol,
        Host             = f.Host,
        Port             = f.Port,
        Format           = f.Format,
        SeverityFilter   = f.SeverityFilter,
        SourceFilter     = f.SourceFilter,
        Enabled          = f.Enabled,
        TlsCertPath      = f.TlsCertPath,
        Description      = f.Description,
        ForwardedCount   = f.ForwardedCount,
        ErrorCount       = f.ErrorCount,
        LastForwardedAt  = f.LastForwardedAt,
        CreatedAt        = f.CreatedAt,
        UpdatedAt        = f.UpdatedAt
    };
}
