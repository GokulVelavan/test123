using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class SourceGroupManager : ISourceGroupManager
{
    private readonly ISourceGroupRepository _repo;
    public SourceGroupManager(ISourceGroupRepository repo) => _repo = repo;

    public Task<List<SourceGroupDto>> GetAllAsync()                                       => _repo.GetAllAsync();
    public Task<SourceGroupDto?>      GetByIdAsync(long id)                               => _repo.GetByIdAsync(id);
    public Task<SourceGroupDto>       CreateAsync(CreateSourceGroupRequest request)       => _repo.CreateAsync(request);
    public Task<SourceGroupDto?>      UpdateAsync(long id, UpdateSourceGroupRequest req)  => _repo.UpdateAsync(id, req);
    public Task<bool>                 DeleteAsync(long id)                                => _repo.DeleteAsync(id);
    public Task<bool>                 UpdateMembersAsync(long groupId, List<long> ids)    => _repo.UpdateMembersAsync(groupId, ids);
}
