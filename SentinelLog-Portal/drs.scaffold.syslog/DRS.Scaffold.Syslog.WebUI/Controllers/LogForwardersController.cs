using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

[Route("log-forwarders")]
public class LogForwardersController : AuthenticatedController
{
    public LogForwardersController(ApiClient api) : base(api) { }

    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index() => View();
}
