using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface ILogForwarderRepository
{
    Task<List<LogForwarderDto>> GetAllAsync();
    Task<LogForwarderDto?>      GetByIdAsync(long id);
    Task<LogForwarderDto>       CreateAsync(CreateLogForwarderRequest request);
    Task<LogForwarderDto?>      UpdateAsync(long id, UpdateLogForwarderRequest request);
    Task<bool>                  DeleteAsync(long id);
    Task<LogForwarderDto?>      ToggleAsync(long id, bool enabled);
}
