using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

public interface IUserRepository
{
    Task<List<UserDto>>  GetAllAsync();
    Task<UserDto?>       GetByIdAsync(long id);
    Task<UserDto>        CreateAsync(CreateUserRequest request);
    Task<UserDto?>       UpdateAsync(long id, UpdateUserRequest request);
    Task<bool>           DeleteAsync(long id);
    Task<UserDto?>       GetByUsernameAsync(string username);
    /// <summary>
    /// Returns the raw <see cref="AppUser"/> entity including the password hash.
    /// Used exclusively by login � never expose the returned entity to callers outside auth.
    /// </summary>
    Task<AppUser?> GetEntityByUsernameAsync(string username);
    /// <summary>Stamps <c>lastlogin</c> for the given user ID.</summary>
    Task UpdateLastLoginAsync(long id);
    /// <summary>#94 — replaces all roles for a user with the given role names.</summary>
    Task<UserDto?> SetRolesAsync(long userId, List<string> roleNames);
}

public interface ISystemLogRepository
{
    Task<PagedResult<SystemLogDto>> GetAllAsync(SystemLogFilterRequest filter);
}
