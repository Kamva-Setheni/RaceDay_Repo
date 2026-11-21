using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Helpers;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public abstract class BaseController : Controller
{
    /// <summary>The logged-in user, read from the session (null when nobody is logged in).</summary>
    protected SessionUser? CurrentUser => HttpContext.Session.GetCurrentUser();

    protected IActionResult RedirectToRoleHome(string role) =>
        role == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");

    /// <summary>The API no longer recognises the session: clear it and ask the user to log in again.</summary>
    protected IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        TempData["Error"] = "Your session has expired. Please log in again.";
        return RedirectToAction("Login", "Account");
    }

    /// <summary>
    /// Handles the failures every page treats the same way (not logged in, not allowed, not found).
    /// Returns null when the call succeeded or when the page should show its own error message.
    /// </summary>
    protected IActionResult? GlobalFailure(ApiResult result)
    {
        if (result.Success) return null;
        if (result.IsUnauthorized) return SessionExpired();
        if (result.IsForbidden) return RedirectToAction("AccessDenied", "Account");
        if (result.IsNotFound) return NotFound();
        return null;
    }
}
