using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Event enrolments: Participants enter a category, Organisers view who has entered.</summary>
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enrols the logged-in Participant in a category (which also links them to its event).</summary>
    /// <param name="id">The category id.</param>
    /// <response code="201">The new enrolment.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The user is not a Participant.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">Already enrolled in this category.</response>
    [HttpPost("api/categories/{id:int}/enrol")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(int id)
    {
        var userId = CurrentUserId;

        if (!await _db.Categories.AnyAsync(c => c.CategoryId == id))
            return NotFoundProblem($"Category {id} does not exist.");

        if (await _db.Enrolments.AnyAsync(e => e.ParticipantId == userId && e.CategoryId == id))
            return ConflictProblem("You are already enrolled in this category.");

        var enrolment = new Enrolment
        {
            ParticipantId = userId,
            CategoryId = id,
            EnrolmentDate = DateTime.UtcNow,
            Status = "Pending"
        };

        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var response = await Project(_db.Enrolments.AsNoTracking().Where(e => e.EnrolmentId == enrolment.EnrolmentId))
            .FirstAsync();

        return CreatedAtAction(nameof(GetMine), response);
    }

    /// <summary>Lists every event/category the logged-in Participant has entered.</summary>
    /// <response code="200">Array of the participant's enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The user is not a Participant.</response>
    [HttpGet("api/users/me/enrolments")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMine()
    {
        var userId = CurrentUserId;
        var enrolments = await Project(_db.Enrolments.AsNoTracking()
                .Where(e => e.ParticipantId == userId)
                .OrderByDescending(e => e.EnrolmentDate))
            .ToListAsync();

        return Ok(enrolments);
    }

    /// <summary>Lists everyone enrolled in an event. Only the event's Organiser may view this.</summary>
    /// <param name="id">The event id.</param>
    /// <response code="200">Array of the event's enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("api/events/{id:int}/enrolments")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetForEvent(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only view enrolments for your own events.");

        var enrolments = await Project(_db.Enrolments.AsNoTracking()
                .Where(e => e.Category!.EventId == id)
                .OrderBy(e => e.Participant!.FullName))
            .ToListAsync();

        return Ok(enrolments);
    }

    /// <summary>Cancels an enrolment. Only the enrolled Participant may cancel their own entry.</summary>
    /// <param name="id">The enrolment id.</param>
    /// <response code="204">Cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or not the owner of this enrolment.</response>
    /// <response code="404">The enrolment does not exist.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Result)
            .Include(e => e.Payment)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return NotFoundProblem($"Enrolment {id} does not exist.");
        if (enrolment.ParticipantId != CurrentUserId) return ForbiddenProblem("You can only cancel your own enrolments.");

        if (enrolment.Result is not null) _db.Results.Remove(enrolment.Result);
        if (enrolment.Payment is not null) _db.Payments.Remove(enrolment.Payment);
        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static IQueryable<EnrolmentResponse> Project(IQueryable<Enrolment> query) =>
        query.Select(e => new EnrolmentResponse(
            e.EnrolmentId,
            e.ParticipantId,
            e.Participant!.FullName,
            e.CategoryId,
            e.Category!.Name,
            e.Category!.EventId,
            e.Category!.Event!.Name,
            e.EnrolmentDate,
            e.Status));
}
