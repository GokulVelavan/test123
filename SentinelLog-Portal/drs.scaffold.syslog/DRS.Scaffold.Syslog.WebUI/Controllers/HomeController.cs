using System.Diagnostics;
using DRS.Scaffold.Syslog.WebUI.Models;
using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApiClient               _api;

    public HomeController(ILogger<HomeController> logger, ApiClient api)
    {
        _logger = logger;
        _api    = api;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Already authenticated ? jump straight to dashboard
        if (HttpContext.Session.GetString("Token") is not null)
            return RedirectToAction("Index", "Dashboard");

        var vm = new HomepageViewModel();

        try
        {
            // Fire all four requests in parallel � no Bearer token required.
            var settingsTask = _api.GetSettingsAsync();
            var summaryTask  = _api.GetDashboardSummaryAsync();
            var sourcesTask  = _api.GetSourcesSummaryAsync();
            var storageTask  = _api.GetStorageOverviewAsync();

            await Task.WhenAll(settingsTask, summaryTask, sourcesTask, storageTask);

            var settings = settingsTask.Result;
            var summary  = summaryTask.Result;
            var sources  = sourcesTask.Result;
            var storage  = storageTask.Result;

            if (settings is not null)
            {
                vm.PlatformName = settings.PlatformName;
                vm.Organization = settings.Organization ?? "NOC / SOC Platform";
            }

            if (summary is not null)
            {
                vm.TotalEventsToday = summary.TotalEventsToday;
                vm.ActiveSources    = summary.ActiveSources;
                vm.EpsNow           = summary.CurrentEps;
            }

            // Override active-source count with the richer sources summary if available
            if (sources is not null)
            {
                vm.ActiveSources = sources.OnlineCount;
            }

            if (storage is not null)
            {
                vm.CompressionRatio       = storage.CompressionRatio;
                vm.IngestionRateGbPerHour = storage.IngestionRateGbPerHour;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Homepage API prefetch failed � rendering with defaults");
        }

        // Preserve login error forwarded via TempData from AuthController
        if (TempData["LoginError"] is string loginError)
            vm.Error = loginError;

        return View(vm);
    }

    [HttpGet("privacy")]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
