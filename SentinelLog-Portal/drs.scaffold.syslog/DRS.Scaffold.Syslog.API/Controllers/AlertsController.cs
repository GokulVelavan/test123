using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

/// <summary>Alert rule configuration and fired-alert event management.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Alerts")]
public class AlertsController : ControllerBase
{
    private readonly IAlertManager _mgr;
    public AlertsController(IAlertManager mgr) => _mgr = mgr;

    // ?? Rules ?????????????????????????????????????????????????????????????????

    /// <summary>All configured alert rules.</summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(List<AlertRuleDto>), 200)]
    public async Task<IActionResult> GetRules()
        => Ok(await _mgr.GetRulesAsync());

    /// <summary>Single alert rule by ID.</summary>
    [HttpGet("rules/{id:long}")]
    [ProducesResponseType(typeof(AlertRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRule(long id)
    {
        var rule = await _mgr.GetRuleByIdAsync(id);
        return rule is null ? NotFound() : Ok(rule);
    }

    /// <summary>Create a new alert rule.</summary>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(AlertRuleDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateRule([FromBody] CreateAlertRuleRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _mgr.CreateRuleAsync(request);
        return CreatedAtAction(nameof(GetRule), new { id = created.Id }, created);
    }

    /// <summary>Update an existing alert rule.</summary>
    [HttpPut("rules/{id:long}")]
    [ProducesResponseType(typeof(AlertRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] UpdateAlertRuleRequest request)
    {
        var updated = await _mgr.UpdateRuleAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Enable or disable a rule (sets <c>alertrule.enabled</c>).</summary>
    /// <param name="enabled">true = enable, false = disable.</param>
    [HttpPatch("rules/{id:long}/toggle")]
    [ProducesResponseType(typeof(AlertRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ToggleRule(long id, [FromQuery] bool enabled)
    {
        var result = await _mgr.ToggleRuleAsync(id, enabled);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Soft-delete a rule (sets <c>isdeleted = true</c>).</summary>
    [HttpDelete("rules/{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteRule(long id)
    {
        var deleted = await _mgr.DeleteRuleAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    // ?? Events ????????????????????????????????????????????????????????????????

    /// <summary>Paginated fired alert events.</summary>
    /// <param name="severity">Filter by severity: Critical | Warning | Error | Notice | Info</param>
    /// <param name="acknowledged">Filter by acknowledgement state (omit for all).</param>
    [HttpGet("events")]
    [ProducesResponseType(typeof(PagedResult<AlertEventDto>), 200)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? severity     = null,
        [FromQuery] bool?   acknowledged = null,
        [FromQuery] int     page         = 1,
        [FromQuery] int     pageSize     = 20)
        => Ok(await _mgr.GetEventsAsync(severity, acknowledged, page, pageSize));

    /// <summary>Single fired alert event by ID.</summary>
    [HttpGet("events/{id:long}")]
    [ProducesResponseType(typeof(AlertEventDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetEvent(long id)
    {
        var ev = await _mgr.GetEventByIdAsync(id);
        return ev is null ? NotFound() : Ok(ev);
    }

    /// <summary>Acknowledge a fired alert — sets <c>acknowledged=true</c>, <c>acknowledgedby</c> and <c>acknowledgedat</c>.</summary>
    [HttpPatch("events/{id:long}/acknowledge")]
    [ProducesResponseType(typeof(AlertEventDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AcknowledgeEvent(
        long id, [FromBody] AcknowledgeAlertEventRequest request)
    {
        var updated = await _mgr.AcknowledgeEventAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Today's alert counts broken down by severity and acknowledgement state.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AlertSummaryDto), 200)]
    public async Task<IActionResult> GetSummary()
        => Ok(await _mgr.GetSummaryAsync());
}
