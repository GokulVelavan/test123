using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class ScheduledReportRepository : IScheduledReportRepository
{
    private readonly SyslogDbContext _db;
    public ScheduledReportRepository(SyslogDbContext db) => _db = db;

    private static ScheduledReportDto ToDto(ScheduledReport r) => new()
    {
        Id          = r.Id,
        Name        = r.Name,
        Description = r.Description,
        Schedule    = r.Schedule,
        Format      = r.Format,
        Enabled     = r.Enabled,
        LastRunAt   = r.LastRunAt,
        CreatedAt   = r.CreatedAt
    };

    public async Task<List<ScheduledReportDto>> GetAllAsync() =>
        (await _db.ScheduledReports.OrderByDescending(r => r.CreatedAt).ToListAsync()).Select(ToDto).ToList();

    public async Task<ScheduledReportDto> CreateAsync(CreateScheduledReportRequest req)
    {
        var r = new ScheduledReport
        {
            Name        = req.Name,
            Description = req.Description,
            Schedule    = req.Schedule,
            Format      = req.Format,
            Enabled     = req.Enabled
        };
        _db.ScheduledReports.Add(r);
        await _db.SaveChangesAsync();
        return ToDto(r);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var r = await _db.ScheduledReports.FindAsync(id);
        if (r is null) return false;
        r.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleAsync(long id, bool enabled)
    {
        var r = await _db.ScheduledReports.FindAsync(id);
        if (r is null) return false;
        r.Enabled = enabled;
        await _db.SaveChangesAsync();
        return true;
    }
}
