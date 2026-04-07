using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Managers;

public interface ISavedSearchManager
{
    Task<List<SavedSearchDto>> GetAllAsync(long? userId);
    Task<SavedSearchDto?>      GetByIdAsync(long id);
    Task<SavedSearchDto>       CreateAsync(CreateSavedSearchRequest request);
    Task<SavedSearchDto?>      UpdateAsync(long id, UpdateSavedSearchRequest request);
    Task<bool>                 DeleteAsync(long id);
}
