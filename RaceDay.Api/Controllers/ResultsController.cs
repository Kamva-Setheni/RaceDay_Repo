using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Race results: Organisers capture them, Participants view their own.</summary>
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Captures the finish time and position for an enrolment after the event.</summary>
    /// <param name="id">The enrolment id.</param>
    /// <response code="201">The new result.</response>
    /// <response code="400">Invalid data (e.g. no finish time for a "Finished" result).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the enrolment's event.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost("api/enrolments/{id:int}/results")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> Create(int id, ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return NotFoundProblem($"Enrolment {id} does not exist.");
        if (enrolment.Category!.Event!.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only capture results for your own events.");

        var error = Validate(request);
        if (error is not null) return BadRequestProblem(error);

        if (enrolment.Result is not null)
            return ConflictProblem("A result has already been captured for this enrolment. Use PUT /api/results/{id} to correct it.");

        var result = new Result
        {
            EnrolmentId = id,
            FinishTime = request.FinishTime,
            Position = request.Position,
            Status = request.Status
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var response = await Project(_db.Results.AsNoTracking().Where(r => r.ResultId == result.ResultId)).FirstAsync();
        return Created($"/api/events/{enrolment.Category.EventId}/results", response);
    }

    /// <summary>Corrects a previously captured result.</summary>
    /// <param name="id">The result id.</param>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">The result does not exist.</response>
    [HttpPut("api/results/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> Update(int id, ResultRequest request)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);

        if (result is null) return NotFoundProblem($"Result {id} does not exist.");
        if (result.Enrolment!.Category!.Event!.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only edit results for your own events.");

        var error = Validate(request);
        if (error is not null) return BadRequestProblem(error);

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = request.Status;
        await _db.SaveChangesAsync();

        var response = await Project(_db.Results.AsNoTracking().Where(r => r.ResultId == id)).FirstAsync();
        return Ok(response);
    }

    /// <summary>Returns the logged-in Participant's own results across all events.</summary>
    /// <response code="200">Array of the participant's results.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The user is not a Participant.</response>
    [HttpGet("api/users/me/results")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMine()
    {
        var userId = CurrentUserId;
        var results = await Project(_db.Results.AsNoTracking()
                .Where(r => r.Enrolment!.ParticipantId == userId)
                .OrderBy(r => r.Enrolment!.Category!.Event!.Name))
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>Returns the results (leaderboard) for an event. Publicly viewable.</summary>
    /// <param name="id">The event id.</param>
    /// <response code="200">Array of results ordered by position.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("api/events/{id:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetForEvent(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == id))
            return NotFoundProblem($"Event {id} does not exist.");

        var results = await Project(_db.Results.AsNoTracking()
                .Where(r => r.Enrolment!.Category!.EventId == id)
                .OrderBy(r => r.Position == null)
                .ThenBy(r => r.Position))
            .ToListAsync();

        return Ok(results);
    }

    private static string? Validate(ResultRequest request)
    {
        if (request.Status == "Finished" && request.FinishTime is null)
            return "A finishTime is required when the status is 'Finished'.";

        if (request.FinishTime is { } time && time <= TimeSpan.Zero)
            return "finishTime must be greater than zero.";

        return null;
    }

    private static IQueryable<ResultResponse> Project(IQueryable<Result> query) =>
        query.Select(r => new ResultResponse(
            r.ResultId,
            r.EnrolmentId,
            r.Enrolment!.Participant!.FullName,
            r.Enrolment!.Category!.Event!.Name,
            r.Enrolment!.Category!.Name,
            r.FinishTime,
            r.Position,
            r.Status));
}
