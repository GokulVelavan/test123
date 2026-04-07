using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface IScheduledReportRepository
{
    Task<List<ScheduledReportDto>> GetAllAsync();
    Task<ScheduledReportDto>       CreateAsync(CreateScheduledReportRequest req);
    Task<bool>                     DeleteAsync(long id);
    Task<bool>                     ToggleAsync(long id, bool enabled);
}
