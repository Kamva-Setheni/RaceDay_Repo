namespace RaceDay.Web.Helpers;

/// <summary>The logged-in user as remembered by the MVC session.</summary>
public record SessionUser(int UserId, string Role, string FullName, string? ProfilePictureUrl)
{
    public bool IsOrganiser => Role == "Organiser";
    public bool IsParticipant => Role == "Participant";
}

public static class SessionExtensions
{
    /// <summary>Reads the logged-in user from the session, or null when nobody is logged in.</summary>
    public static SessionUser? GetCurrentUser(this ISession session)
    {
        var id = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);
        if (id is null || string.IsNullOrEmpty(role)) return null;

        var picture = session.GetString(SessionKeys.ProfilePictureUrl);
        return new SessionUser(
            id.Value,
            role,
            session.GetString(SessionKeys.FullName) ?? string.Empty,
            string.IsNullOrEmpty(picture) ? null : picture);
    }
}
