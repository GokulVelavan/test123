using BCrypt.Net;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SyslogDbContext _db;
    public UserRepository(SyslogDbContext db) => _db = db;

    public async Task<List<UserDto>> GetAllAsync() =>
        await _db.AppUsers.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username)
            .Select(u => ToDto(u))
            .ToListAsync();

    public async Task<UserDto?> GetByIdAsync(long id)
    {
        var u = await _db.AppUsers.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Id == id);
        return u is null ? null : ToDto(u);
    }

    public async Task<UserDto?> GetByUsernameAsync(string username)
    {
        var u = await _db.AppUsers.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Username == username);
        return u is null ? null : ToDto(u);
    }

    /// <inheritdoc/>
    public async Task<AppUser?> GetEntityByUsernameAsync(string username) =>
        await _db.AppUsers
            .FirstOrDefaultAsync(x => x.Username == username && !x.IsDeleted);

    /// <inheritdoc/>
    public async Task UpdateLastLoginAsync(long id)
    {
        var entity = await _db.AppUsers.FindAsync(id);
        if (entity is null) return;
        entity.LastLogin = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req)
    {
        var entity = new AppUser
        {
            Username     = req.Username,
            Email        = req.Email,
            // Hash the plain-text password before storing
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
            FullName     = req.FullName,
            Active       = true,
            CreatedAt    = DateTimeOffset.UtcNow,
            UpdatedAt    = DateTimeOffset.UtcNow
        };
        _db.AppUsers.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<UserDto?> UpdateAsync(long id, UpdateUserRequest req)
    {
        var entity = await _db.AppUsers
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return null;

        if (req.FullName is not null) entity.FullName = req.FullName;
        if (req.Email    is not null) entity.Email    = req.Email;
        if (req.Active   is not null) entity.Active   = req.Active.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.AppUsers.FindAsync(id);
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // #94 — replace all roles for a user
    public async Task<UserDto?> SetRolesAsync(long userId, List<string> roleNames)
    {
        var user = await _db.AppUsers
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        // Remove existing role assignments
        _db.UserRoles.RemoveRange(user.UserRoles);

        // Find or skip roles by name
        foreach (var rn in roleNames.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == rn);
            if (role is null) continue;
            _db.UserRoles.Add(new UserRole
            {
                UserId    = userId,
                RoleId    = role.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        // Reload to get updated roles
        var updated = await _db.AppUsers.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
        return updated is null ? null : ToDto(updated);
    }

    private static UserDto ToDto(AppUser u) => new()
    {
        Id         = u.Id,
        Username   = u.Username,
        Email      = u.Email,
        FullName   = u.FullName,
        Active     = u.Active,
        LastLogin  = u.LastLogin,
        CreatedAt  = u.CreatedAt,
        MfaEnabled = u.MfaEnabled,
        Roles      = u.UserRoles
                      .Where(ur => ur.Role != null)
                      .Select(ur => ur.Role!.Name)
                      .ToList()
    };
}

// ?? SystemLogRepository ???????????????????????????????????????????????????????
public class SystemLogRepository : ISystemLogRepository
{
    private readonly SyslogDbContext _db;
    public SystemLogRepository(SyslogDbContext db) => _db = db;

    public async Task<PagedResult<SystemLogDto>> GetAllAsync(SystemLogFilterRequest filter)
    {
        var q = _db.SystemLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Host))
            q = q.Where(s => s.Host != null && s.Host.Contains(filter.Host));
        if (!string.IsNullOrWhiteSpace(filter.Program))
            q = q.Where(s => s.Program != null && s.Program.Contains(filter.Program));
        if (!string.IsNullOrWhiteSpace(filter.SourceIp))
            q = q.Where(s => s.SourceIp == filter.SourceIp);
        if (filter.DateFrom.HasValue)
            q = q.Where(s => s.LogDatetime >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            q = q.Where(s => s.LogDatetime <= filter.DateTo.Value);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(s => s.LogDatetime)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new SystemLogDto
            {
                Id          = s.Id,
                LogDatetime = s.LogDatetime,
                Host        = s.Host,
                Program     = s.Program,
                Pid         = s.Pid,
                Message     = s.Message,
                SourceIp    = s.SourceIp
            })
            .ToListAsync();

        return new PagedResult<SystemLogDto>
        {
            Items = items, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize
        };
    }
}
