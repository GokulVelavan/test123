using DRS.Scaffold.Syslog.WebUI.Models;
using DRS.Scaffold.Syslog.WebUI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DRS.Scaffold.Syslog.WebUI.Controllers;

/// <summary>
/// Base controller that injects the session Bearer token into every
/// outgoing API call and redirects unauthenticated users to login.
/// </summary>
public abstract class AuthenticatedController : Controller
{
    protected readonly ApiClient Api;
    protected string CurrentUsername  => HttpContext.Session.GetString("Username")  ?? "—";
    protected string CurrentFullName  => HttpContext.Session.GetString("FullName")  ?? "—";
    protected string CurrentRole      => HttpContext.Session.GetString("Role")       ?? "—";

    protected AuthenticatedController(ApiClient api) => Api = api;

    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        var token = HttpContext.Session.GetString("Token");
        if (string.IsNullOrEmpty(token))
        {
            ctx.Result = RedirectToAction("Index", "Home");
            return;
        }
        Api.SetBearerToken(token);

        // Pass user info to all views via ViewBag
        ViewBag.Username = CurrentUsername;
        ViewBag.FullName = CurrentFullName;
        ViewBag.Role     = CurrentRole;
        ViewBag.Initials = Initials(CurrentFullName);

        base.OnActionExecuting(ctx);
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}"
            : name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
    }
}
