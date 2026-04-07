using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class LicenseManager : ILicenseManager
{
    private readonly SyslogDbContext _db;
    public LicenseManager(SyslogDbContext db) => _db = db;

    public async Task<LicenseDto?> GetActiveLicenseAsync()
    {
        var entity = await _db.Licenses.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.ActivatedAt)
            .FirstOrDefaultAsync();
        return entity is null ? null : ToDto(entity);
    }

    public async Task<LicenseDto> ActivateAsync(ActivateLicenseRequest request)
    {
        var existing = await _db.Licenses.Where(l => l.IsActive).ToListAsync();
        foreach (var e in existing) e.IsActive = false;

        var entity = new License
        {
            LicenseKey  = request.LicenseKey,
            Licensee    = !string.IsNullOrWhiteSpace(request.Licensee) ? request.Licensee : "SentinelLog User",
            Plan        = "Professional",
            MaxSources  = 1000,
            MaxEps       = 50000,
            ExpiresAt   = DateTimeOffset.UtcNow.AddYears(1),
            IsActive    = true,
            ActivatedAt = DateTimeOffset.UtcNow,
            CreatedAt   = DateTimeOffset.UtcNow
        };
        _db.Licenses.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<bool> DeactivateAsync(long id)
    {
        var entity = await _db.Licenses.FindAsync(id);
        if (entity is null) return false;
        entity.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    private static LicenseDto ToDto(License l) => new()
    {
        Id          = l.Id,
        Licensee    = l.Licensee,
        Plan        = l.Plan,
        MaxSources  = l.MaxSources,
        MaxEps      = l.MaxEps,
        ExpiresAt   = l.ExpiresAt,
        ActivatedAt = l.ActivatedAt,
        IsActive    = l.IsActive
    };
}
