using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Categories (e.g. Under 20, Senior, 10km, 21km) within an event.</summary>
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Lists the categories available for an event. Publicly viewable.</summary>
    /// <param name="id">The event id.</param>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("api/events/{id:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetForEvent(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == id))
            return NotFoundProblem($"Event {id} does not exist.");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == id)
            .OrderBy(c => c.Distance).ThenBy(c => c.Name)
            .ToListAsync();

        return Ok(categories.Select(ToResponse));
    }

    /// <summary>Adds a category to an event. Only the event's Organiser may do this.</summary>
    /// <param name="id">The event id.</param>
    /// <response code="201">The new category.</response>
    /// <response code="400">Missing or invalid fields.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPost("api/events/{id:int}/categories")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Create(int id, CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only add categories to your own events.");

        var category = new Category
        {
            EventId = id,
            Name = request.Name.Trim(),
            Distance = request.Distance,
            EntryFee = request.EntryFee
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{id}/categories", ToResponse(category));
    }

    /// <summary>Updates a category. Only the owning event's Organiser may do this.</summary>
    /// <param name="id">The category id.</param>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Missing or invalid fields.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpPut("api/categories/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundProblem($"Category {id} does not exist.");
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only edit categories of your own events.");

        category.Name = request.Name.Trim();
        category.Distance = request.Distance;
        category.EntryFee = request.EntryFee;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(category));
    }

    /// <summary>Removes a category (and any enrolments in it). Only the event's Organiser may do this.</summary>
    /// <param name="id">The category id.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpDelete("api/categories/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundProblem($"Category {id} does not exist.");
        if (category.Event!.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete categories of your own events.");

        await _db.RemoveEnrolmentsForCategoriesAsync(new List<int> { id });
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static CategoryResponse ToResponse(Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.Distance, c.EntryFee);
}
