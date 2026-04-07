using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

[ApiController]
[Route("api/anomaly")]
[Produces("application/json")]
[Tags("Anomaly")]
public class AnomalyController : ControllerBase
{
    private readonly IAnomalyManager _mgr;
    public AnomalyController(IAnomalyManager mgr) => _mgr = mgr;

    // ── Rules ─────────────────────────────────────────────────────────────────

    /// <summary>List all anomaly detection rules.</summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(List<AnomalyRuleDto>), 200)]
    public async Task<IActionResult> GetRules()
        => Ok(await _mgr.GetRulesAsync());

    /// <summary>Get a single anomaly rule.</summary>
    [HttpGet("rules/{id:long}")]
    [ProducesResponseType(typeof(AnomalyRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRule(long id)
    {
        var r = await _mgr.GetRuleByIdAsync(id);
        return r is null ? NotFound() : Ok(r);
    }

    /// <summary>Create a new anomaly detection rule.</summary>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(AnomalyRuleDto), 201)]
    public async Task<IActionResult> CreateRule([FromBody] CreateAnomalyRuleRequest req)
        => StatusCode(201, await _mgr.CreateRuleAsync(req));

    /// <summary>Update an anomaly rule.</summary>
    [HttpPut("rules/{id:long}")]
    [ProducesResponseType(typeof(AnomalyRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] UpdateAnomalyRuleRequest req)
    {
        var r = await _mgr.UpdateRuleAsync(id, req);
        return r is null ? NotFound() : Ok(r);
    }

    /// <summary>Delete an anomaly rule.</summary>
    [HttpDelete("rules/{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteRule(long id)
        => await _mgr.DeleteRuleAsync(id) ? NoContent() : NotFound();

    /// <summary>Enable or disable an anomaly rule.</summary>
    [HttpPatch("rules/{id:long}/toggle")]
    [ProducesResponseType(typeof(AnomalyRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ToggleRule(long id, [FromQuery] bool enabled)
    {
        var r = await _mgr.ToggleRuleAsync(id, enabled);
        return r is null ? NotFound() : Ok(r);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>List fired anomaly events.</summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(PagedResult<AnomalyEventDto>), 200)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] bool? acknowledged = null,
        [FromQuery] int   page         = 1,
        [FromQuery] int   pageSize     = 25)
        => Ok(await _mgr.GetEventsAsync(acknowledged, page, pageSize));

    /// <summary>Acknowledge an anomaly event.</summary>
    [HttpPatch("events/{id:long}/acknowledge")]
    [ProducesResponseType(typeof(AnomalyEventDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Acknowledge(long id, [FromQuery] string acknowledgedBy = "system")
    {
        var e = await _mgr.AcknowledgeEventAsync(id, acknowledgedBy);
        return e is null ? NotFound() : Ok(e);
    }

    /// <summary>Anomaly detection summary counts.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AnomalySummaryDto), 200)]
    public async Task<IActionResult> Summary()
        => Ok(await _mgr.GetSummaryAsync());
}
