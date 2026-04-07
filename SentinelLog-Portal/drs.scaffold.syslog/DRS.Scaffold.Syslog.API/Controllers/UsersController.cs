using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>User account management (appuser table).</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Users")]
public class UsersController : ControllerBase
{
    private readonly IUserManager _mgr;
    public UsersController(IUserManager mgr) => _mgr = mgr;

    /// <summary>All user accounts with their assigned roles.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserDto>), 200)]
    public async Task<IActionResult> GetAll()
        => Ok(await _mgr.GetUsersAsync());

    /// <summary>Single user by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var user = await _mgr.GetUserByIdAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Create a new user account.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _mgr.CreateUserAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Update a user's full name, email or active status.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateUserRequest request)
    {
        var updated = await _mgr.UpdateUserAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Soft-delete a user account (sets <c>isdeleted = true</c>).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _mgr.DeleteUserAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>#94 — Replace user role assignments. Pass an empty array to remove all roles.</summary>
    [HttpPut("{id:long}/roles")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetRoles(long id, [FromBody] AssignRolesRequest request)
    {
        var updated = await _mgr.SetRolesAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }
}
