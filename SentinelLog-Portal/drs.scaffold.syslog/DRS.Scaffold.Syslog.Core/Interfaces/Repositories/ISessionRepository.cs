using DRS.Scaffold.Syslog.Core.Models;

namespace DRS.Scaffold.Syslog.Core.Interfaces.Repositories;

/// <summary>Manages <c>usersession</c> rows — one row per active refresh token.</summary>
public interface ISessionRepository
{
    /// <summary>Persists a new session row after successful login.</summary>
    Task<UserSession> CreateAsync(
        long userId, string refreshToken, DateTimeOffset expiresAt,
        string? ipAddress, string? userAgent);

    /// <summary>Looks up an active (non-revoked, non-expired) session by its refresh token.</summary>
    Task<UserSession?> GetActiveByTokenAsync(string refreshToken);

    /// <summary>Marks a single session as revoked (logout).</summary>
    Task RevokeAsync(string refreshToken);

    /// <summary>Revokes all sessions for a user (e.g. password change, force-sign-out).</summary>
    Task RevokeAllForUserAsync(long userId);
}
