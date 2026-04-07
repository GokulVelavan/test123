using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>#58/#61 — Notification channel CRUD (Email, SNMP, Webhook, Audible).</summary>
[ApiController]
[Route("api/notification-channels")]
[Produces("application/json")]
[Tags("Settings")]
public class NotificationChannelsController : ControllerBase
{
    private readonly INotificationChannelManager _mgr;
    public NotificationChannelsController(INotificationChannelManager mgr) => _mgr = mgr;

    [HttpGet]
    [ProducesResponseType(typeof(List<NotificationChannelDto>), 200)]
    public async Task<IActionResult> GetAll() => Ok(await _mgr.GetAllAsync());

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(NotificationChannelDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var ch = await _mgr.GetByIdAsync(id);
        return ch is null ? NotFound() : Ok(ch);
    }

    [HttpPost]
    [ProducesResponseType(typeof(NotificationChannelDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateNotificationChannelRequest request)
    {
        var created = await _mgr.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(NotificationChannelDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateNotificationChannelRequest request)
    {
        var updated = await _mgr.UpdateAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
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
    [ProducesResponseType(typeof(NotificationChannelDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Toggle(long id, [FromQuery] bool enabled)
    {
        var ch = await _mgr.ToggleEnabledAsync(id, enabled);
        return ch is null ? NotFound() : Ok(ch);
    }
}
