using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class SavedSearchManager : ISavedSearchManager
{
    private readonly ISavedSearchRepository _repo;
    public SavedSearchManager(ISavedSearchRepository repo) => _repo = repo;

    public Task<List<SavedSearchDto>> GetAllAsync(long? userId) => _repo.GetAllAsync(userId);
    public Task<SavedSearchDto?> GetByIdAsync(long id)          => _repo.GetByIdAsync(id);
    public Task<SavedSearchDto> CreateAsync(CreateSavedSearchRequest req) => _repo.CreateAsync(req);
    public Task<SavedSearchDto?> UpdateAsync(long id, UpdateSavedSearchRequest req) => _repo.UpdateAsync(id, req);
    public Task<bool> DeleteAsync(long id)                      => _repo.DeleteAsync(id);
}
