using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class LogForwarderManager : ILogForwarderManager
{
    private readonly ILogForwarderRepository _repo;
    public LogForwarderManager(ILogForwarderRepository repo) => _repo = repo;

    public Task<List<LogForwarderDto>> GetAllAsync()       => _repo.GetAllAsync();
    public Task<LogForwarderDto?> GetByIdAsync(long id)    => _repo.GetByIdAsync(id);
    public Task<LogForwarderDto> CreateAsync(CreateLogForwarderRequest req) => _repo.CreateAsync(req);
    public Task<LogForwarderDto?> UpdateAsync(long id, UpdateLogForwarderRequest req) => _repo.UpdateAsync(id, req);
    public Task<bool> DeleteAsync(long id)                 => _repo.DeleteAsync(id);
    public Task<LogForwarderDto?> ToggleAsync(long id, bool enabled) => _repo.ToggleAsync(id, enabled);
}
