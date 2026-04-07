using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>Authentication — login, token refresh, logout and MFA verification.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Auth")]
public class AuthController : ControllerBase
{
    private readonly IUserManager _mgr;
    public AuthController(IUserManager mgr) => _mgr = mgr;

    /// <summary>Authenticate with username and password.</summary>
    /// <remarks>
    /// On success returns a signed JWT (<c>token</c>) valid for the configured
    /// access-token lifetime, plus an opaque <c>refreshToken</c> stored in
    /// the <c>usersession</c> table.  Pass the JWT as
    /// <c>Authorization: Bearer {token}</c> on subsequent requests.
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(LoginResponse), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip        = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _mgr.LoginAsync(request, ip, userAgent);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Exchange a valid refresh token for a new access + refresh token pair.</summary>
    /// <remarks>
    /// Each refresh token is single-use — the old session row is revoked and a
    /// new <c>usersession</c> row is written on every successful refresh.
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(typeof(LoginResponse), 401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var ip        = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _mgr.RefreshAsync(request.RefreshToken, ip, userAgent);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Revoke a refresh token — signs the session out server-side.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var revoked = await _mgr.LogoutAsync(request.RefreshToken);
        return revoked ? NoContent() : BadRequest(new { message = "Session not found or already revoked." });
    }

    /// <summary>Verify a 6-digit TOTP/OTP code from the MFA screen.</summary>
    /// <remarks>Demo: accepts any 6-digit numeric code. Production must validate against a stored TOTP secret.</remarks>
    [HttpPost("verify-mfa")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    public IActionResult VerifyMfa([FromBody] VerifyMfaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 6)
            return BadRequest(new { success = false, message = "Invalid OTP code." });

        return Ok(new { success = true, message = "MFA verified." });
    }
}

/// <summary>MFA verification request body.</summary>
public class VerifyMfaRequest
{
    /// <summary>6-digit OTP code.</summary>
    public string  Code     { get; set; } = string.Empty;
    public string? Username { get; set; }
}
