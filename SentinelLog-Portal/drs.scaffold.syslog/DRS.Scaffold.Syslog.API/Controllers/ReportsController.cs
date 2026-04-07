using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
[Tags("Reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportsManager _mgr;
    public ReportsController(IReportsManager mgr) => _mgr = mgr;

    [HttpGet]
    [ProducesResponseType(typeof(List<ScheduledReportDto>), 200)]
    public async Task<IActionResult> GetAll() => Ok(await _mgr.GetScheduledReportsAsync());

    [HttpPost]
    [ProducesResponseType(typeof(ScheduledReportDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateScheduledReportRequest req)
    {
        var result = await _mgr.CreateAsync(req);
        return StatusCode(201, result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _mgr.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:long}/toggle")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Toggle(long id, [FromQuery] bool enabled)
    {
        var ok = await _mgr.ToggleAsync(id, enabled);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Generate a compliance summary report.</summary>
    [HttpGet("compliance")]
    [ProducesResponseType(typeof(ComplianceReportDto), 200)]
    public async Task<IActionResult> Compliance(
        [FromQuery] string framework = "pci-dss",
        [FromQuery] int    days      = 30)
    {
        var result = await _mgr.GenerateComplianceReportAsync(framework, days);
        return Ok(result);
    }

    /// <summary>Run a scheduled report on-demand and return the file as a download.</summary>
    [HttpGet("{id:long}/run")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RunReport(long id)
    {
        var file = await _mgr.RunReportAsync(id);
        if (file is null) return NotFound();
        return File(file.Value.Content, file.Value.ContentType, file.Value.FileName);
    }
}
