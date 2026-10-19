using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Services;

/// <summary>
/// Authenticates every request from the server-side session. When a user logs in, their id and
/// role are stored in the session on the server; the browser only holds the session cookie.
/// This handler reads the id and role back out of the session and turns them into the user's
/// identity, so [Authorize] and [Authorize(Roles = "...")] work on all subsequent requests.
/// </summary>
public class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "RaceDaySession";

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        await Context.Session.LoadAsync();

        var userId = Context.Session.GetInt32(SessionKeys.UserId);
        var role = Context.Session.GetString(SessionKeys.Role);

        // No logged-in user in this session: anonymous request ([Authorize] will then return 401)
        if (userId is null || string.IsNullOrEmpty(role))
        {
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
