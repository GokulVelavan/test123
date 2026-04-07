using DRS.Scaffold.Syslog.WebUI.Models;
using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

/// <summary>Maps to sentinellog-sources-settings.html — Sources inventory and Settings pages.</summary>
[Route("sources")]
public class SourcesController : AuthenticatedController
{
    public SourcesController(ApiClient api) : base(api) { }

    // GET /sources
    [HttpGet("")]
    [HttpGet("inventory")]
    public IActionResult Index() => View();

    // GET /sources/groups
    [HttpGet("groups")]
    public IActionResult Groups() => View();

    // GET /sources/protocols
    [HttpGet("protocols")]
    public IActionResult Protocols() => View();

    // GET /sources/parsers
    [HttpGet("parsers")]
    public IActionResult Parsers() => View();
}
