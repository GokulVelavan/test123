using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>#97-98 — API key management for programmatic platform access.</summary>
[ApiController]
[Route("api/api-keys")]
[Produces("application/json")]
[Tags("Settings")]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyManager _mgr;
    public ApiKeysController(IApiKeyManager mgr) => _mgr = mgr;

    [HttpGet]
    [ProducesResponseType(typeof(List<ApiKeyDto>), 200)]
    public async Task<IActionResult> GetAll() => Ok(await _mgr.GetAllAsync());

    /// <summary>Create a new API key. The full key is returned ONCE in the response.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResponse), 201)]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request)
    {
        var result = await _mgr.CreateAsync(request);
        return StatusCode(201, result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _mgr.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPatch("{id:long}/toggle")]
    [ProducesResponseType(typeof(ApiKeyDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Toggle(long id, [FromQuery] bool enabled)
    {
        var key = await _mgr.ToggleEnabledAsync(id, enabled);
        return key is null ? NotFound() : Ok(key);
    }
}
