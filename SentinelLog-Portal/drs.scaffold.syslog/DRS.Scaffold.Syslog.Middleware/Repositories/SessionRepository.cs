using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Middleware.Repositories;

/// <inheritdoc/>
public class SessionRepository : ISessionRepository
{
    private readonly SyslogDbContext _db;
    public SessionRepository(SyslogDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<UserSession> CreateAsync(
        long userId, string refreshToken, DateTimeOffset expiresAt,
        string? ipAddress, string? userAgent)
    {
        var session = new UserSession
        {
            UserId       = userId,
            RefreshToken = refreshToken,
            IpAddress    = ipAddress,
            UserAgent    = userAgent,
            ExpiresAt    = expiresAt,
            Revoked      = false,
            CreatedAt    = DateTimeOffset.UtcNow,
            UpdatedAt    = DateTimeOffset.UtcNow,
            CreatedBy    = "system",
            IsDeleted    = false
        };
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    /// <inheritdoc/>
    public async Task<UserSession?> GetActiveByTokenAsync(string refreshToken) =>
        await _db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s =>
                s.RefreshToken == refreshToken &&
                !s.Revoked                    &&
                s.ExpiresAt > DateTimeOffset.UtcNow);

    /// <inheritdoc/>
    public async Task RevokeAsync(string refreshToken)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);
        if (session is null) return;

        session.Revoked   = true;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task RevokeAllForUserAsync(long userId)
    {
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && !s.Revoked)
            .ToListAsync();

        foreach (var s in sessions)
        {
            s.Revoked   = true;
            s.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync();
    }
}
