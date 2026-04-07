using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>Source group management — organise log sources into named collections.</summary>
[ApiController]
[Route("api/source-groups")]
[Produces("application/json")]
[Tags("Sources")]
public class SourceGroupsController : ControllerBase
{
    private readonly ISourceGroupManager _mgr;
    public SourceGroupsController(ISourceGroupManager mgr) => _mgr = mgr;

    /// <summary>List all source groups with their member source IDs.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SourceGroupDto>), 200)]
    public async Task<IActionResult> GetAll() => Ok(await _mgr.GetAllAsync());

    /// <summary>Get a single source group by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SourceGroupDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var g = await _mgr.GetByIdAsync(id);
        return g is null ? NotFound() : Ok(g);
    }

    /// <summary>Create a new source group.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SourceGroupDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateSourceGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");
        var result = await _mgr.CreateAsync(request);
        return StatusCode(201, result);
    }

    /// <summary>Update an existing source group's name, description, and color.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(SourceGroupDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSourceGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");
        var result = await _mgr.UpdateAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Delete a source group (soft-delete).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _mgr.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Replace the full set of source members for a group.</summary>
    [HttpPut("{id:long}/members")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateMembers(long id, [FromBody] UpdateGroupMembersRequest request)
    {
        var ok = await _mgr.UpdateMembersAsync(id, request.SourceIds ?? new());
        return ok ? NoContent() : NotFound();
    }
}
