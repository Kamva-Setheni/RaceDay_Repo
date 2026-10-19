using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using AppUser = RaceDay.Api.Models.User;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout.</summary>
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Registers a new Organiser or Participant account.</summary>
    /// <remarks>
    /// Anyone can register as a Participant. To stop people simply choosing the Organiser role,
    /// registering as an Organiser also requires the organiser sign-up code (organiserCode).
    /// The password is hashed with BCrypt and never stored in its original form.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user (never the password).</response>
    /// <response code="400">Missing or invalid fields.</response>
    /// <response code="403">Organiser role requested without a valid organiser sign-up code.</response>
    /// <response code="409">The email address is already registered.</response>
    [HttpPost("api/auth/register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        if (request.Role == AppRoles.Organiser)
        {
            var expectedCode = _config["OrganiserSignupCode"];
            if (string.IsNullOrWhiteSpace(expectedCode) || request.OrganiserCode != expectedCode)
            {
                return ForbiddenProblem("A valid organiser sign-up code is required to register as an Organiser.");
            }
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return ConflictProblem("An account with this email address already exists.");
        }

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created("/api/users/me", ToResponse(user));
    }

    /// <summary>Logs in and starts a server-side session that stores the user's id and role.</summary>
    /// <remarks>
    /// The server keeps the user's id and role in the session. The response sets an HttpOnly session
    /// cookie (.RaceDay.Session), which the browser (and Swagger UI) sends automatically on every later
    /// request, so the user stays authenticated until they log out or the session expires.
    /// </remarks>
    /// <response code="200">Login successful. Returns the user; the session cookie is set.</response>
    /// <response code="400">Missing or invalid fields.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("api/auth/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !PasswordMatches(request.Password, user.PasswordHash))
        {
            return UnauthorizedProblem("Invalid email or password.");
        }

        // Start a fresh session and remember who is logged in and what role they have
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);

        return Ok(ToResponse(user));
    }

    /// <summary>Logs out by ending the server-side session.</summary>
    /// <response code="204">Logged out. The session no longer holds a user.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("api/auth/logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    private static bool PasswordMatches(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception)
        {
            // The stored value is not a valid BCrypt hash (e.g. placeholder seed data)
            return false;
        }
    }

    private static UserResponse ToResponse(AppUser u) =>
        new(u.UserId, u.FullName, u.Email, u.Role, u.CreatedAt);
}
