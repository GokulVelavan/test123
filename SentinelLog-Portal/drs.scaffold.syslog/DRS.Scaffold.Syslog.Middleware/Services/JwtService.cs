using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DRS.Scaffold.Syslog.Core.Options;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DRS.Scaffold.Syslog.Middleware.Services;

/// <summary>
/// Generates signed JWT access tokens and cryptographically random
/// refresh tokens.  Stateless — session persistence is handled by
/// <see cref="DRS.Scaffold.Syslog.Middleware.Repositories.SessionRepository"/>.
/// </summary>
public class JwtService
{
    private readonly JwtSettings _cfg;

    public JwtService(IOptions<JwtSettings> opts) => _cfg = opts.Value;

    // ?? Access token ??????????????????????????????????????????????????????????

    /// <summary>
    /// Builds a signed JWT containing the user's <c>sub</c>, <c>name</c>,
    /// <c>email</c> and <c>role</c> claims.
    /// </summary>
    public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(UserDto user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg.Key));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddMinutes(_cfg.AccessTokenMins);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        // Add one claim per role so ASP.NET Core [Authorize(Roles = "Admin")] works
        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             _cfg.Issuer,
            audience:           _cfg.Audience,
            claims:             claims,
            notBefore:          DateTimeOffset.UtcNow.UtcDateTime,
            expires:            expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    // ?? Refresh token ?????????????????????????????????????????????????????????

    /// <summary>
    /// Returns a 64-byte cryptographically random Base64Url string and its
    /// expiry timestamp.  The value is stored (hashed) in <c>usersession</c>.
    /// </summary>
    public (string Token, DateTimeOffset ExpiresAt) GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return (
            Convert.ToBase64String(bytes),
            DateTimeOffset.UtcNow.AddDays(_cfg.RefreshTokenDays));
    }

    // ?? Validation (for /refresh endpoint) ???????????????????????????????????

    /// <summary>
    /// Validates an expired access token and returns its principal so the
    /// <c>/refresh</c> endpoint can issue a new one without re-authenticating.
    /// Returns <c>null</c> if the token signature is invalid.
    /// </summary>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateIssuerSigningKey  = true,
            ValidateLifetime         = false,   // allow expired tokens
            ValidIssuer              = _cfg.Issuer,
            ValidAudience            = _cfg.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(_cfg.Key))
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, parameters, out var secToken);

            if (secToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
