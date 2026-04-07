using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface ISourceGroupRepository
{
    Task<List<SourceGroupDto>> GetAllAsync();
    Task<SourceGroupDto?>      GetByIdAsync(long id);
    Task<SourceGroupDto>       CreateAsync(CreateSourceGroupRequest request);
    Task<SourceGroupDto?>      UpdateAsync(long id, UpdateSourceGroupRequest request);
    Task<bool>                 DeleteAsync(long id);
    Task<bool>                 UpdateMembersAsync(long groupId, List<long> sourceIds);
}
