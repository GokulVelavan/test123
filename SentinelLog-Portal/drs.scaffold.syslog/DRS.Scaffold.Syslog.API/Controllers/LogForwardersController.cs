using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

[ApiController]
[Route("api/log-forwarders")]
[Produces("application/json")]
[Tags("LogForwarders")]
public class LogForwardersController : ControllerBase
{
    private readonly ILogForwarderManager _mgr;
    public LogForwardersController(ILogForwarderManager mgr) => _mgr = mgr;

    /// <summary>List all configured log forwarder destinations.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<LogForwarderDto>), 200)]
    public async Task<IActionResult> GetAll()
        => Ok(await _mgr.GetAllAsync());

    /// <summary>Get a single forwarder by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(LogForwarderDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _mgr.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Add a new log forwarding destination.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LogForwarderDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateLogForwarderRequest req)
    {
        var result = await _mgr.CreateAsync(req);
        return StatusCode(201, result);
    }

    /// <summary>Update an existing forwarder.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(LogForwarderDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateLogForwarderRequest req)
    {
        var result = await _mgr.UpdateAsync(id, req);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Delete a forwarder.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _mgr.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Enable or disable a forwarder.</summary>
    [HttpPatch("{id:long}/toggle")]
    [ProducesResponseType(typeof(LogForwarderDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Toggle(long id, [FromQuery] bool enabled)
    {
        var result = await _mgr.ToggleAsync(id, enabled);
        return result is null ? NotFound() : Ok(result);
    }
}
