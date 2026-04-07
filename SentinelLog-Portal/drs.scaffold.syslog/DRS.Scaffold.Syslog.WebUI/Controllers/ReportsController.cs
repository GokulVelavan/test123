using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

[Route("reports")]
public class ReportsController : AuthenticatedController
{
    public ReportsController(ApiClient api) : base(api) { }

    [HttpGet("")]
    [HttpGet("scheduled")]
    public IActionResult Scheduled() => View();

    [HttpGet("compliance")]
    public IActionResult Compliance() => View();

    [HttpGet("exports")]
    public IActionResult Exports() => View();
}
