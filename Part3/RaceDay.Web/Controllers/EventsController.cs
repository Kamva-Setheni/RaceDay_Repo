using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Event information (public) and entering an event (Participants only).</summary>
public class EventsController : BaseController
{
    private readonly RaceDayApiClient _api;

    public EventsController(RaceDayApiClient api) => _api = api;

    /// <summary>Full event information and the categories a Participant can enter.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (ev.IsNotFound) return NotFound();
        if (!ev.Success)
        {
            TempData["Error"] = ev.Error;
            return RedirectToAction("Index", "Home");
        }

        var categories = await _api.GetCategoriesAsync(id);
        var model = new EventDetailsViewModel
        {
            Event = ev.Data!,
            Categories = categories.Success ? categories.Data ?? new List<CategoryDto>() : new List<CategoryDto>()
        };

        if (!categories.Success) ViewBag.CategoryError = categories.Error;

        var user = CurrentUser;
        if (user?.IsParticipant == true)
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            if (mine.IsUnauthorized) return SessionExpired();
            if (mine.Success)
            {
                model.EnrolledCategoryIds = mine.Data!.Select(e => e.CategoryId).ToHashSet();
            }
        }

        model.IsOwner = user?.IsOrganiser == true && ev.Data!.OrganiserId == user.UserId;
        return View(model);
    }

    /// <summary>Enters the logged-in Participant into a category. The API records it immediately.</summary>
    [HttpPost, SessionAuthorize(Role = "Participant")]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        var result = await _api.EnrolAsync(categoryId);
        if (GlobalFailure(result) is { } early) return early;

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        TempData["Success"] = "You're in! Your enrolment has been recorded.";
        return RedirectToAction("MyEvents", "Participant");
    }
}
