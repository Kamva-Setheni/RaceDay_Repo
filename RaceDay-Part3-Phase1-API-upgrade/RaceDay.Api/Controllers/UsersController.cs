using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;
using AppUser = RaceDay.Api.Models.User;

namespace RaceDay.Api.Controllers;

/// <summary>The logged-in user's own profile (both roles).</summary>
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public UsersController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        _db = db;
        _blobs = blobs;
    }

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

    /// <summary>Uploads (or replaces) the logged-in user's profile picture, stored in Azure Blob Storage.</summary>
    /// <remarks>Send the image as multipart/form-data in a field called "file". JPG, PNG, GIF or WEBP, up to 5 MB.
    /// The blob's URL is saved on the user and returned as profilePictureUrl.</remarks>
    /// <param name="file">The image file.</param>
    /// <response code="200">The updated profile, with its new profilePictureUrl.</response>
    /// <response code="400">No file, wrong file type, not a real image, or larger than 5 MB.</response>
    /// <response code="401">Not logged in (no valid session).</response>
    /// <response code="503">Azure Blob Storage is not available.</response>
    [HttpPost("api/users/me/profile-picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserResponse>> UploadProfilePicture(IFormFile file)
    {
        var userId = CurrentUserId;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return UnauthorizedProblem("The account for this session no longer exists.");

        var check = await ImageValidator.ValidateAsync(file);
        if (!check.IsValid) return BadRequestProblem(check.Error!);

        string url;
        try
        {
            await using var stream = file.OpenReadStream();
            url = await _blobs.UploadImageAsync(BlobContainers.ProfilePictures,
                $"user-{userId}-{Guid.NewGuid():N}{check.Extension}", stream, check.ContentType);
        }
        catch (StorageUnavailableException)
        {
            return ServiceUnavailableProblem("Image storage is not available right now. Please try again later.");
        }

        var oldUrl = user.ProfilePictureUrl;
        user.ProfilePictureUrl = url;
        await _db.SaveChangesAsync();
        await _blobs.DeleteByUrlAsync(oldUrl);

        return Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(AppUser u) =>
        new(u.UserId, u.FullName, u.Email, u.Role, u.CreatedAt, u.ProfilePictureUrl);
}
