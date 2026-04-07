using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface ISavedSearchRepository
{
    Task<List<SavedSearchDto>> GetAllAsync(long? userId);
    Task<SavedSearchDto?>      GetByIdAsync(long id);
    Task<SavedSearchDto>       CreateAsync(CreateSavedSearchRequest request);
    Task<SavedSearchDto?>      UpdateAsync(long id, UpdateSavedSearchRequest request);
    Task<bool>                 DeleteAsync(long id);
}
