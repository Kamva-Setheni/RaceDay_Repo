using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    /// <summary>The person's full name.</summary>
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Unique email address, used to log in.</summary>
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored only as a BCrypt hash.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required, RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Role must be either 'Organiser' or 'Participant'.")]
    public string Role { get; set; } = string.Empty;

    /// <summary>Required only when registering as an Organiser (the organiser sign-up code).</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public record UserResponse(int UserId, string FullName, string Email, string Role, DateTime CreatedAt);
