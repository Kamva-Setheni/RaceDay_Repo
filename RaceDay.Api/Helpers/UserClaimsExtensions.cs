using System.Security.Claims;

namespace RaceDay.Api.Helpers;

public static class UserClaimsExtensions
{
    /// <summary>Reads the logged-in user's id (stored in the server-side session).</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The session does not contain a valid user id.");
    }
}
