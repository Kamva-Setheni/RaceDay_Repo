using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Events. Everyone can view them; only the owning Organiser can change them.</summary>
public class EventsController : ApiControllerBase
{
    private static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };

    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public EventsController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        _db = db;
        _blobs = blobs;
    }

    /// <summary>Lists events, soonest first, with optional filters. Publicly browsable.</summary>
    /// <param name="eventType">Only events of this type: Run, Walk or Cycle.</param>
    /// <param name="search">Only events whose name or location contains this text.</param>
    /// <param name="upcomingOnly">When true, hides events that have already taken place.</param>
    /// <param name="from">Only events on or after this date.</param>
    /// <param name="to">Only events on or before this date.</param>
    /// <response code="200">Array of events.</response>
    /// <response code="400">eventType is not Run, Walk or Cycle.</response>
    [HttpGet("api/events")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetAll(
        [FromQuery] string? eventType = null,
        [FromQuery] string? search = null,
        [FromQuery] bool upcomingOnly = false,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            if (!EventTypes.Contains(eventType))
                return BadRequestProblem("eventType must be 'Run', 'Walk' or 'Cycle'.");

            query = query.Where(e => e.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Name.Contains(term) || e.Location.Contains(term));
        }

        if (upcomingOnly)
        {
            var today = DateTime.Today;
            query = query.Where(e => e.EventDate >= today);
        }

        if (from.HasValue) query = query.Where(e => e.EventDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.EventDate <= to.Value);

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(ToResponse));
    }

    /// <summary>Organiser dashboard data: the logged-in Organiser's own events with category and enrolment counts.</summary>
    /// <response code="200">Array of the organiser's events, soonest first.</response>
    /// <response code="401">Not logged in (no valid session).</response>
    /// <response code="403">The user is not an Organiser.</response>
    [HttpGet("api/events/mine")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<OrganiserEventSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<OrganiserEventSummary>>> GetMine()
    {
        var userId = CurrentUserId;

        var events = await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == userId)
            .OrderBy(e => e.EventDate)
            .Select(e => new OrganiserEventSummary(
                e.EventId,
                e.Name,
                e.EventDate,
                e.Location,
                e.EventType,
                e.Distance,
                e.BannerImageUrl,
                e.Categories.Count,
                _db.Enrolments.Count(en => en.Category!.EventId == e.EventId)))
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>Returns the details of a single event.</summary>
    /// <param name="id">The event id.</param>
    /// <response code="200">The event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("api/events/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");

        return Ok(ToResponse(ev));
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">The new event.</response>
    /// <response code="400">Missing or invalid fields (e.g. eventType must be Run, Walk or Cycle).</response>
    /// <response code="401">Not logged in (no valid session).</response>
    /// <response code="403">The user is not an Organiser.</response>
    [HttpPost("api/events")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create(EventRequest request)
    {
        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            Distance = request.Distance,
            EventType = request.EventType,
            CreatedAt = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ToResponse(ev));
    }

    /// <summary>Updates an event. Only the Organiser who owns it may do this.</summary>
    /// <param name="id">The event id.</param>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Missing or invalid fields.</response>
    /// <response code="401">Not logged in (no valid session).</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPut("api/events/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(int id, EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only edit your own events.");

        ev.Name = request.Name.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.Distance = request.Distance;
        ev.EventType = request.EventType;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(ev));
    }

    /// <summary>Uploads (or replaces) the event's banner image, stored in Azure Blob Storage.</summary>
    /// <remarks>Send the image as multipart/form-data in a field called "file". JPG, PNG, GIF or WEBP, up to 5 MB.
    /// The blob's URL is saved on the event and returned as bannerImageUrl.</remarks>
    /// <param name="id">The event id.</param>
    /// <param name="file">The image file.</param>
    /// <response code="200">The event, with its new bannerImageUrl.</response>
    /// <response code="400">No file, wrong file type, not a real image, or larger than 5 MB.</response>
    /// <response code="401">Not logged in (no valid session).</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="503">Azure Blob Storage is not available.</response>
    [HttpPost("api/events/{id:int}/banner")]
    [Authorize(Roles = AppRoles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EventResponse>> UploadBanner(int id, IFormFile file)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only change the banner of your own events.");

        var check = await ImageValidator.ValidateAsync(file);
        if (!check.IsValid) return BadRequestProblem(check.Error!);

        string url;
        try
        {
            await using var stream = file.OpenReadStream();
            url = await _blobs.UploadImageAsync(BlobContainers.EventBanners,
                $"event-{id}-{Guid.NewGuid():N}{check.Extension}", stream, check.ContentType);
        }
        catch (StorageUnavailableException)
        {
            return ServiceUnavailableProblem("Image storage is not available right now. Please try again later.");
        }

        var oldUrl = ev.BannerImageUrl;
        ev.BannerImageUrl = url;
        await _db.SaveChangesAsync();
        await _blobs.DeleteByUrlAsync(oldUrl);

        return Ok(ToResponse(ev));
    }

    /// <summary>Deletes an event together with its categories and their enrolments.</summary>
    /// <param name="id">The event id.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not logged in (no valid session).</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpDelete("api/events/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete your own events.");

        var bannerUrl = ev.BannerImageUrl;

        await _db.RemoveEnrolmentsForCategoriesAsync(ev.Categories.Select(c => c.CategoryId).ToList());
        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        await _blobs.DeleteByUrlAsync(bannerUrl);
        return NoContent();
    }

    private static EventResponse ToResponse(Event e) =>
        new(e.EventId, e.OrganiserId, e.Name, e.Description, e.EventDate, e.Location,
            e.Distance, e.EventType, e.CreatedAt, e.BannerImageUrl);
}
