using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

[ApiController]
[Route("api/incidents")]
[Produces("application/json")]
[Tags("Incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentManager _mgr;
    public IncidentsController(IIncidentManager mgr) => _mgr = mgr;

    /// <summary>List incidents with optional status/priority filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<IncidentDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status   = null,
        [FromQuery] string? priority = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 25)
        => Ok(await _mgr.GetAllAsync(status, priority, page, pageSize));

    /// <summary>Get a single incident by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(IncidentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _mgr.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new security incident.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IncidentDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateIncidentRequest req)
    {
        var result = await _mgr.CreateAsync(req);
        return StatusCode(201, result);
    }

    /// <summary>Update an existing incident.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(IncidentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateIncidentRequest req)
    {
        var result = await _mgr.UpdateAsync(id, req);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Soft-delete an incident.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _mgr.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Get incident summary counts.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IncidentSummaryDto), 200)]
    public async Task<IActionResult> Summary()
        => Ok(await _mgr.GetSummaryAsync());
}
