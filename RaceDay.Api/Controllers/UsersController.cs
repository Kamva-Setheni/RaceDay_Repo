using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>The logged-in user's own profile (both roles).</summary>
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db) => _db = db;

    /// <summary>Returns the profile of the currently logged-in user.</summary>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not logged in (no valid session).</response>
    [HttpGet("api/users/me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var userId = CurrentUserId;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return UnauthorizedProblem("The account for this session no longer exists.");

        return Ok(ToResponse(user));
    }

    /// <summary>Updates the logged-in user's own name and email.</summary>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Missing or invalid fields.</response>
    /// <response code="401">Not logged in (no valid session).</response>
    /// <response code="409">The email address belongs to another account.</response>
    [HttpPut("api/users/me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> UpdateMe(UpdateProfileRequest request)
    {
        var userId = CurrentUserId;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return UnauthorizedProblem("The account for this session no longer exists.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.UserId != userId))
        {
            return ConflictProblem("This email address belongs to another account.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(User u) =>
        new(u.UserId, u.FullName, u.Email, u.Role, u.CreatedAt);
}
