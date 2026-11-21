using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Web.Helpers;

/// <summary>
/// Protects a page using the MVC session. Visitors who are not logged in are redirected to the
/// login page; users with the wrong role are sent to the "access denied" page.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>Optional role required for the page: "Organiser" or "Participant".</summary>
    public string? Role { get; set; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync();
        var user = session.GetCurrentUser();

        if (user is null)
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Method == "GET" ? $"{request.Path}{request.QueryString}" : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (Role is not null && user.Role != Role)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
