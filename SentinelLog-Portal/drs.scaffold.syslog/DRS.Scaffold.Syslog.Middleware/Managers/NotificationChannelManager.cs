using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using DRS.Scaffold.Syslog.Middleware.Repositories;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class NotificationChannelManager : INotificationChannelManager
{
    private readonly NotificationChannelRepository _repo;
    public NotificationChannelManager(NotificationChannelRepository repo) => _repo = repo;

    public Task<List<NotificationChannelDto>>  GetAllAsync()                                                          => _repo.GetAllAsync();
    public Task<NotificationChannelDto?>       GetByIdAsync(long id)                                                  => _repo.GetByIdAsync(id);
    public Task<NotificationChannelDto>        CreateAsync(CreateNotificationChannelRequest request)                  => _repo.CreateAsync(request);
    public Task<NotificationChannelDto?>       UpdateAsync(long id, UpdateNotificationChannelRequest request)         => _repo.UpdateAsync(id, request);
    public Task<bool>                          DeleteAsync(long id)                                                    => _repo.DeleteAsync(id);
    public Task<NotificationChannelDto?>       ToggleEnabledAsync(long id, bool enabled)                              => _repo.ToggleEnabledAsync(id, enabled);
}
