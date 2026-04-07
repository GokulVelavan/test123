using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>#101-102 — License key activation and management.</summary>
[ApiController]
[Route("api/license")]
[Produces("application/json")]
[Tags("Settings")]
public class LicenseController : ControllerBase
{
    private readonly ILicenseManager _mgr;
    public LicenseController(ILicenseManager mgr) => _mgr = mgr;

    /// <summary>Get the currently active license.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(LicenseDto), 200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Get()
    {
        var license = await _mgr.GetActiveLicenseAsync();
        return license is null ? NoContent() : Ok(license);
    }

    /// <summary>Activate a new license key.</summary>
    [HttpPost("activate")]
    [ProducesResponseType(typeof(LicenseDto), 200)]
    public async Task<IActionResult> Activate([FromBody] ActivateLicenseRequest request)
    {
        var result = await _mgr.ActivateAsync(request);
        return Ok(result);
    }

    /// <summary>Deactivate a license by ID.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deactivate(long id)
    {
        var ok = await _mgr.DeactivateAsync(id);
        return ok ? NoContent() : NotFound();
    }
}
