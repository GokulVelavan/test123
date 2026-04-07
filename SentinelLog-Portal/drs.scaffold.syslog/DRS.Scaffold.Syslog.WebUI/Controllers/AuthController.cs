using DRS.Scaffold.Syslog.WebUI.Models;
using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly ApiClient _api;
    public AuthController(ApiClient api) => _api = api;

    // POST /auth/login � called from the homepage login card
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/Home/Index.cshtml", new HomepageViewModel { Username = vm.Username, Error = "Invalid input." });

        var result = await _api.LoginAsync(new LoginRequest
        {
            Username = vm.Username,
            Password = vm.Password
        });

        if (result is null || !result.Success)
        {
            var hvm = new HomepageViewModel
            {
                Username = vm.Username,
                Error    = result?.Message ?? "Invalid credentials. Please try again."
            };
            return View("~/Views/Home/Index.cshtml", hvm);
        }

        // Store tokens and user info in server-side session
        HttpContext.Session.SetString("Token",        result.Token ?? "");
        HttpContext.Session.SetString("RefreshToken", result.RefreshToken ?? "");
        HttpContext.Session.SetString("Username",     result.User?.Username ?? vm.Username);
        HttpContext.Session.SetString("FullName",     result.User?.FullName ?? vm.Username);
        HttpContext.Session.SetString("Role",         result.User?.Roles.FirstOrDefault() ?? "");

        return RedirectToAction("Index", "Dashboard");
    }

    // POST /auth/logout
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var rt = HttpContext.Session.GetString("RefreshToken");
        if (!string.IsNullOrEmpty(rt))
            await _api.LogoutAsync(rt);

        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
