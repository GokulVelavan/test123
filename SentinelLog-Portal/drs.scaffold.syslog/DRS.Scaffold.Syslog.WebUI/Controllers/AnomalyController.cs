using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

[Route("anomaly")]
public class AnomalyController : AuthenticatedController
{
    public AnomalyController(ApiClient api) : base(api) { }

    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index() => View();
}
