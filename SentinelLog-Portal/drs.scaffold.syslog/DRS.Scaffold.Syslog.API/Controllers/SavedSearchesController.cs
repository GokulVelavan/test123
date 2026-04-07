using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

[ApiController]
[Route("api/saved-searches")]
[Produces("application/json")]
[Tags("SavedSearches")]
public class SavedSearchesController : ControllerBase
{
    private readonly ISavedSearchManager _mgr;
    public SavedSearchesController(ISavedSearchManager mgr) => _mgr = mgr;

    /// <summary>List saved searches. Pass userId to include owner-private searches.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SavedSearchDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] long? userId = null)
        => Ok(await _mgr.GetAllAsync(userId));

    /// <summary>Get a single saved search.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SavedSearchDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _mgr.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Save a new search query.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SavedSearchDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateSavedSearchRequest req)
    {
        var result = await _mgr.CreateAsync(req);
        return StatusCode(201, result);
    }

    /// <summary>Update a saved search.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(SavedSearchDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSavedSearchRequest req)
    {
        var result = await _mgr.UpdateAsync(id, req);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Delete a saved search.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _mgr.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
}
