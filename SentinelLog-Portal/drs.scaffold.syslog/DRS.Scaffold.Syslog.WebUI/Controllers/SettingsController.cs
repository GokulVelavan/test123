using DRS.Scaffold.Syslog.WebUI.Models;
using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

[Route("settings")]
public class SettingsController : AuthenticatedController
{
    public SettingsController(ApiClient api) : base(api) { }

    // GET /settings
    [HttpGet("")]
    [HttpGet("general")]
    public async Task<IActionResult> General()
    {
        var dto = await Api.GetSettingsAsync();
        var vm = dto is null ? new PlatformSettingsViewModel() : new PlatformSettingsViewModel
        {
            PlatformName          = dto.PlatformName,
            Organization          = dto.Organization,
            ContactEmail          = dto.ContactEmail,
            Timezone              = dto.Timezone,
            DateFormat            = dto.DateFormat,
            ShowMilliseconds      = dto.ShowMilliseconds,
            AutoScrollFeed        = dto.AutoScrollFeed,
            AudibleAlerts         = dto.AudibleAlerts,
            FeedBufferSize        = dto.FeedBufferSize,
            RefreshIntervalSeconds = dto.RefreshIntervalSeconds
        };
        return View(vm);
    }

    // POST /settings/general
    [HttpPost("general")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GeneralSave(PlatformSettingsViewModel vm)
    {
        await Api.UpdateSettingsAsync(new
        {
            vm.PlatformName,
            vm.Organization,
            vm.ContactEmail,
            vm.Timezone,
            vm.DateFormat,
            vm.ShowMilliseconds,
            vm.AutoScrollFeed,
            vm.AudibleAlerts,
            vm.FeedBufferSize,
            vm.RefreshIntervalSeconds
        });
        TempData["Notice"] = "Settings saved.";
        return RedirectToAction(nameof(General));
    }

    // GET /settings/users
    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await Api.GetUsersAsync() ?? new();
        return View(new UsersManagementViewModel { Users = users });
    }

    // POST /settings/users/delete/{id}
    [HttpPost("users/delete/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(long id)
    {
        await Api.DeleteUserAsync(id);
        return RedirectToAction(nameof(Users));
    }

    // GET /settings/network
    [HttpGet("network")]
    public IActionResult Network() => View();

    // GET /settings/notifications
    [HttpGet("notifications")]
    public IActionResult Notifications() => View();

    // GET /settings/api
    [HttpGet("api")]
    public IActionResult ApiKeys() => View();

    // GET /settings/sessions
    [HttpGet("sessions")]
    public IActionResult Sessions() => View();

    // GET /settings/audit
    [HttpGet("audit")]
    public IActionResult Audit() => View();

    // GET /settings/license
    [HttpGet("license")]
    public IActionResult License() => View();
}
