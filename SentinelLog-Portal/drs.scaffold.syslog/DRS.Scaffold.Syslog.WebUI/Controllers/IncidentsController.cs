using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

[Route("incidents")]
public class IncidentsController : AuthenticatedController
{
    public IncidentsController(ApiClient api) : base(api) { }

    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index() => View();
}
