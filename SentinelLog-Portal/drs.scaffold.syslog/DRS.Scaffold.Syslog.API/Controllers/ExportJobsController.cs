using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.API.Controllers;

[ApiController]
[Route("api/export-jobs")]
[Produces("application/json")]
[Tags("ExportJobs")]
public class ExportJobsController : ControllerBase
{
    private readonly SyslogDbContext _db;
    public ExportJobsController(SyslogDbContext db) => _db = db;

    /// <summary>List all export jobs (most recent first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ExportJobDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var q = _db.ExportJobs.OrderByDescending(j => j.CreatedAt);
        var total = await q.CountAsync();
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new ExportJobDto
            {
                Id              = j.Id,
                UserId          = j.UserId,
                QueryDefinition = j.QueryDefinition,
                Format          = j.Format,
                Status          = j.Status,
                FilePath        = j.FilePath,
                CreatedAt       = j.CreatedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<ExportJobDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize });
    }

    /// <summary>Get a single export job by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ExportJobDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var j = await _db.ExportJobs.FindAsync(id);
        if (j is null || j.IsDeleted) return NotFound();
        return Ok(new ExportJobDto
        {
            Id              = j.Id,
            UserId          = j.UserId,
            QueryDefinition = j.QueryDefinition,
            Format          = j.Format,
            Status          = j.Status,
            FilePath        = j.FilePath,
            CreatedAt       = j.CreatedAt
        });
    }

    /// <summary>Queue a new export job.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExportJobDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateExportJobRequest req)
    {
        var job = new ExportJob
        {
            UserId          = req.UserId,
            QueryDefinition = req.QueryDefinition,
            Format          = req.Format,
            Status          = "Queued",
            CreatedAt       = DateTimeOffset.UtcNow
        };
        _db.ExportJobs.Add(job);
        await _db.SaveChangesAsync();
        return StatusCode(201, new ExportJobDto
        {
            Id              = job.Id,
            UserId          = job.UserId,
            QueryDefinition = job.QueryDefinition,
            Format          = job.Format,
            Status          = job.Status,
            CreatedAt       = job.CreatedAt
        });
    }

    /// <summary>Download the completed export file.</summary>
    [HttpGet("{id:long}/download")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Download(long id)
    {
        var j = await _db.ExportJobs.FindAsync(id);
        if (j is null || j.IsDeleted) return NotFound();
        if (j.Status != "Completed" || string.IsNullOrEmpty(j.FilePath))
            return Conflict(new { message = $"Export job is '{j.Status}' — file not ready yet." });
        // Prevent path traversal — ensure file is inside the exports directory
        var exportDir  = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "exports"));
        var fullPath   = Path.GetFullPath(j.FilePath);
        if (!fullPath.StartsWith(exportDir, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid file path." });

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Export file not found on disk." });

        var ext         = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext == ".json" ? "application/json" : "text/csv";
        var fileName    = Path.GetFileName(fullPath);
        var stream      = System.IO.File.OpenRead(fullPath);
        return File(stream, contentType, fileName);
    }

    /// <summary>Cancel / delete an export job.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        var j = await _db.ExportJobs.FindAsync(id);
        if (j is null || j.IsDeleted) return NotFound();
        j.IsDeleted = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
