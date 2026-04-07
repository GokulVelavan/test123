using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.API.Controllers;

[ApiController]
[Route("api/threat-intel")]
[Produces("application/json")]
[Tags("ThreatIntel")]
public class ThreatIntelController : ControllerBase
{
    private readonly IThreatIntelManager _mgr;
    public ThreatIntelController(IThreatIntelManager mgr) => _mgr = mgr;

    /// <summary>List threat indicators with optional type/active filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ThreatIndicatorDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type     = null,
        [FromQuery] bool?   active   = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50)
        => Ok(await _mgr.GetAllAsync(type, active, page, pageSize));

    /// <summary>Get a single indicator by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ThreatIndicatorDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _mgr.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Add a new threat indicator (IP, domain, hash, etc.).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ThreatIndicatorDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateThreatIndicatorRequest req)
    {
        var result = await _mgr.CreateAsync(req);
        return StatusCode(201, result);
    }

    /// <summary>Update an existing indicator.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ThreatIndicatorDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateThreatIndicatorRequest req)
    {
        var result = await _mgr.UpdateAsync(id, req);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Delete an indicator.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _mgr.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Check if a value (IP / domain / hash) matches any active threat indicator.</summary>
    [HttpGet("check")]
    [ProducesResponseType(typeof(ThreatCheckResultDto), 200)]
    public async Task<IActionResult> Check([FromQuery] string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return BadRequest("value is required");
        var result = await _mgr.CheckValueAsync(value);
        return Ok(result);
    }

    /// <summary>Threat intel summary counts.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ThreatIntelSummaryDto), 200)]
    public async Task<IActionResult> Summary()
        => Ok(await _mgr.GetSummaryAsync());

    /// <summary>
    /// Bulk import indicators from CSV text.
    /// Expected CSV header: Type,Value,ThreatCategory,Confidence,Source,Description
    /// </summary>
    [HttpPost("import/csv")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ImportCsv([FromBody] BulkImportCsvRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CsvContent))
            return BadRequest("CsvContent is required.");

        var lines   = req.CsvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int imported = 0, skipped = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Type", StringComparison.OrdinalIgnoreCase)) continue; // header
            var cols = line.Split(',');
            if (cols.Length < 2) { skipped++; continue; }

            var type  = cols[0].Trim();
            var value = cols[1].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(value)) { skipped++; continue; }

            await _mgr.CreateAsync(new CreateThreatIndicatorRequest
            {
                Type           = string.IsNullOrWhiteSpace(type) ? "IP" : type,
                Value          = value,
                ThreatCategory = cols.Length > 2 ? cols[2].Trim().Trim('"') : null,
                Confidence     = cols.Length > 3 && int.TryParse(cols[3].Trim(), out var conf) ? conf : 80,
                Source         = cols.Length > 4 ? cols[4].Trim().Trim('"') : req.DefaultSource,
                Description    = cols.Length > 5 ? cols[5].Trim().Trim('"') : null,
                IsActive       = true
            });
            imported++;
        }

        return Ok(new { imported, skipped });
    }
}
