using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>Log source (device) inventory � CRUD and status management.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Sources")]
public class SourcesController : ControllerBase
{
    private readonly ILogSourceManager _mgr;
    public SourcesController(ILogSourceManager mgr) => _mgr = mgr;

    /// <summary>Paginated list of all registered log sources.</summary>
    /// <param name="search">Filter by name, IP address or location (contains).</param>
    /// <param name="status">Online | Offline | Degraded | Pending</param>
    /// <param name="deviceType">Firewall | Router | Switch | Linux | Windows | IDS/IPS | Other</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LogSourceDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search     = null,
        [FromQuery] string? status     = null,
        [FromQuery] string? deviceType = null,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 20)
        => Ok(await _mgr.GetSourcesAsync(search, status, deviceType, page, pageSize));

    /// <summary>Status summary � total, online, offline, degraded, pending counts.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(LogSourceSummaryDto), 200)]
    public async Task<IActionResult> GetSummary()
        => Ok(await _mgr.GetSummaryAsync());

    /// <summary>Single source by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(LogSourceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var source = await _mgr.GetSourceByIdAsync(id);
        return source is null ? NotFound() : Ok(source);
    }

    /// <summary>Register a new log source.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LogSourceDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateLogSourceRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _mgr.AddSourceAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Update an existing source (name, IP, device type, location, OS type).</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(LogSourceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateLogSourceRequest request)
    {
        var updated = await _mgr.UpdateSourceAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Set the source status. Valid values: Online | Offline | Degraded | Pending.</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(typeof(LogSourceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetStatus(long id, [FromQuery] string status)
    {
        var updated = await _mgr.UpdateSourceAsync(id, new UpdateLogSourceRequest { Status = status });
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Soft-delete a source (sets isdeleted = true).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _mgr.RemoveSourceAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>#72 — Bulk soft-delete sources by ID list.</summary>
    [HttpPost("bulk-delete")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> BulkDelete([FromBody] BulkSourceIdsRequest request)
    {
        var count = await _mgr.BulkDeleteAsync(request.Ids);
        return Ok(new { deleted = count });
    }

    /// <summary>#72 — Bulk set status on sources by ID list.</summary>
    [HttpPost("bulk-status")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> BulkSetStatus([FromBody] BulkSourceStatusRequest request)
    {
        var count = await _mgr.BulkSetStatusAsync(request.Ids, request.Status);
        return Ok(new { updated = count });
    }

    /// <summary>#18/#22/#23 — Time-series event count for a specific source.</summary>
    /// <param name="id">Source ID.</param>
    /// <param name="hours">Look-back hours (default 24).</param>
    [HttpGet("{id:long}/activity")]
    [ProducesResponseType(typeof(List<SourceActivityDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetActivity(long id, [FromQuery] int hours = 24)
    {
        if (await _mgr.GetSourceByIdAsync(id) is null)
            return NotFound();
        var data = await _mgr.GetActivityAsync(id, hours);
        return Ok(data);
    }
}
