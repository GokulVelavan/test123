using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

[Route("threat-intel")]
public class ThreatIntelController : AuthenticatedController
{
    public ThreatIntelController(ApiClient api) : base(api) { }

    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index() => View();
}
