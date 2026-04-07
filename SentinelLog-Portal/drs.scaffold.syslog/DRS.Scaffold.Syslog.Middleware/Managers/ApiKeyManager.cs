using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Managers;

public class ApiKeyManager : IApiKeyManager
{
    private readonly SyslogDbContext _db;
    public ApiKeyManager(SyslogDbContext db) => _db = db;

    public async Task<List<ApiKeyDto>> GetAllAsync() =>
        await _db.ApiKeys.AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => ToDto(k))
            .ToListAsync();

    public async Task<CreateApiKeyResponse> CreateAsync(CreateApiKeyRequest request)
    {
        var rawKey  = "sk_" + Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                              .Replace("+", "a").Replace("/", "b").Replace("=", "c");
        var prefix  = rawKey[..8];
        var keyHash = BCrypt.Net.BCrypt.HashPassword(rawKey, workFactor: 10);

        var entity = new ApiKey
        {
            Name        = request.Name,
            Description = request.Description,
            KeyHash     = keyHash,
            KeyPrefix   = prefix,
            Enabled     = true,
            ExpiresAt   = request.ExpiresAt,
            CreatedAt   = DateTimeOffset.UtcNow
        };
        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync();

        return new CreateApiKeyResponse
        {
            Id          = entity.Id,
            Name        = entity.Name,
            Description = entity.Description,
            KeyPrefix   = prefix,
            Enabled     = true,
            ExpiresAt   = entity.ExpiresAt,
            CreatedAt   = entity.CreatedAt,
            PlainKey    = rawKey
        };
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.ApiKeys.FindAsync(id);
        if (entity is null) return false;
        entity.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ApiKeyDto?> ToggleEnabledAsync(long id, bool enabled)
    {
        var entity = await _db.ApiKeys.FindAsync(id);
        if (entity is null) return null;
        entity.Enabled = enabled;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    private static ApiKeyDto ToDto(ApiKey k) => new()
    {
        Id          = k.Id,
        Name        = k.Name,
        Description = k.Description,
        KeyPrefix   = k.KeyPrefix,
        Enabled     = k.Enabled,
        ExpiresAt   = k.ExpiresAt,
        LastUsedAt  = k.LastUsedAt,
        CreatedAt   = k.CreatedAt
    };
}
