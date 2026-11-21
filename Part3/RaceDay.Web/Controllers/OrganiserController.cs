using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Pages for logged-in Organisers. The API checks ownership on every change.</summary>
[SessionAuthorize(Role = "Organiser")]
public class OrganiserController : BaseController
{
    private static readonly Regex TimePattern = new(@"^\d{1,2}:\d{2}:\d{2}$", RegexOptions.Compiled);
    private readonly RaceDayApiClient _api;

    public OrganiserController(RaceDayApiClient api) => _api = api;

    // ================= Dashboard and event list =================

    public async Task<IActionResult> Dashboard()
    {
        var result = await _api.GetMyEventsAsync();
        if (GlobalFailure(result) is { } early) return early;

        return View(new OrganiserDashboardViewModel
        {
            Events = result.Data ?? new List<OrganiserEventDto>(),
            Error = result.Success ? null : result.Error
        });
    }

    public async Task<IActionResult> Events()
    {
        var result = await _api.GetMyEventsAsync();
        if (GlobalFailure(result) is { } early) return early;

        return View(new OrganiserDashboardViewModel
        {
            Events = result.Data ?? new List<OrganiserEventDto>(),
            Error = result.Success ? null : result.Error
        });
    }

    // ================= Create, edit and delete events =================

    [HttpGet]
    public IActionResult CreateEvent() =>
        View("EventForm", new EventFormViewModel { EventDate = DateTime.Today.AddDays(30).AddHours(6) });

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("EventForm", model);

        var created = await _api.CreateEventAsync(ToInput(model));
        if (GlobalFailure(created) is { } early) return early;

        if (!created.Success)
        {
            ModelState.AddModelError(string.Empty, created.Error ?? "We could not create the event.");
            return View("EventForm", model);
        }

        var eventId = created.Data!.EventId;
        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await _api.UploadBannerAsync(eventId, model.BannerFile);
            if (GlobalFailure(upload) is { } uploadFailure) return uploadFailure;

            if (!upload.Success)
            {
                TempData["Error"] = $"The event was created, but its banner could not be uploaded: {upload.Error}";
                return RedirectToAction(nameof(EditEvent), new { id = eventId });
            }
        }

        TempData["Success"] = "Event created. Now add the categories people can enter.";
        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (GlobalFailure(ev) is { } early) return early;

        if (!ev.Success)
        {
            TempData["Error"] = ev.Error;
            return RedirectToAction(nameof(Events));
        }

        if (ev.Data!.OrganiserId != CurrentUser!.UserId) return RedirectToAction("AccessDenied", "Account");
        return View("EventForm", ToForm(ev.Data));
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;
        if (!ModelState.IsValid) return View("EventForm", model);

        var updated = await _api.UpdateEventAsync(id, ToInput(model));
        if (GlobalFailure(updated) is { } early) return early;

        if (!updated.Success)
        {
            ModelState.AddModelError(string.Empty, updated.Error ?? "We could not update the event.");
            model.ExistingBannerUrl = (await _api.GetEventAsync(id)).Data?.BannerImageUrl;
            return View("EventForm", model);
        }

        if (model.BannerFile is { Length: > 0 })
        {
            var upload = await _api.UploadBannerAsync(id, model.BannerFile);
            if (GlobalFailure(upload) is { } uploadFailure) return uploadFailure;

            if (!upload.Success)
            {
                TempData["Error"] = $"The event details were saved, but the banner could not be uploaded: {upload.Error}";
                return RedirectToAction(nameof(EditEvent), new { id });
            }
        }

        TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await _api.DeleteEventAsync(id);
        if (GlobalFailure(result) is { } early) return early;

        if (result.Success) TempData["Success"] = "The event and its enrolments have been deleted.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Events));
    }

    // ================= Categories =================

    [HttpGet]
    public async Task<IActionResult> Categories(int id)
    {
        var model = await BuildCategoriesAsync(id);
        if (model.Failure is not null) return model.Failure;
        return View(model.Page);
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
            return RedirectToAction(nameof(Categories), new { id = model.EventId });
        }

        var result = await _api.CreateCategoryAsync(model.EventId, new CategoryInput(model.Name.Trim(), model.Distance, model.EntryFee));
        if (GlobalFailure(result) is { } early) return early;

        if (result.Success) TempData["Success"] = $"Category \"{model.Name.Trim()}\" added.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Categories), new { id = model.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id, int eventId)
    {
        var categories = await _api.GetCategoriesAsync(eventId);
        if (GlobalFailure(categories) is { } early) return early;

        var category = categories.Data?.FirstOrDefault(c => c.CategoryId == id);
        if (category is null) return NotFound();

        return View(new CategoryFormViewModel
        {
            CategoryId = category.CategoryId,
            EventId = category.EventId,
            Name = category.Name,
            Distance = category.Distance,
            EntryFee = category.EntryFee
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.UpdateCategoryAsync(model.CategoryId, new CategoryInput(model.Name.Trim(), model.Distance, model.EntryFee));
        if (GlobalFailure(result) is { } early) return early;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We could not update the category.");
            return View(model);
        }

        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Categories), new { id = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        var result = await _api.DeleteCategoryAsync(id);
        if (GlobalFailure(result) is { } early) return early;

        if (result.Success) TempData["Success"] = "Category deleted.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    // ================= Enrolments =================

    public async Task<IActionResult> Enrolments(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (GlobalFailure(ev) is { } early) return early;
        if (!ev.Success) { TempData["Error"] = ev.Error; return RedirectToAction(nameof(Events)); }

        var enrolments = await _api.GetEventEnrolmentsAsync(id);
        if (GlobalFailure(enrolments) is { } enrolmentFailure) return enrolmentFailure;

        if (!enrolments.Success) ViewBag.Error = enrolments.Error;
        return View(new EnrolmentsViewModel
        {
            Event = ev.Data!,
            Enrolments = enrolments.Data ?? new List<EnrolmentDto>()
        });
    }

    // ================= Results =================

    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (GlobalFailure(ev) is { } early) return early;
        if (!ev.Success) { TempData["Error"] = ev.Error; return RedirectToAction(nameof(Events)); }

        var enrolments = await _api.GetEventEnrolmentsAsync(id);
        if (GlobalFailure(enrolments) is { } enrolmentFailure) return enrolmentFailure;

        var results = await _api.GetEventResultsAsync(id);
        if (GlobalFailure(results) is { } resultFailure) return resultFailure;

        if (!enrolments.Success) ViewBag.Error = enrolments.Error;
        var byEnrolment = (results.Data ?? new List<ResultDto>()).ToDictionary(r => r.EnrolmentId);

        var rows = (enrolments.Data ?? new List<EnrolmentDto>())
            .OrderBy(e => e.CategoryName).ThenBy(e => e.ParticipantName)
            .Select(e => new ResultRowViewModel
            {
                Enrolment = e,
                Result = byEnrolment.TryGetValue(e.EnrolmentId, out var r) ? r : null
            })
            .ToList();

        return View(new ResultsViewModel { Event = ev.Data!, Rows = rows });
    }

    /// <summary>Captures (or corrects) one participant's finish time and position. Results are visible to the participant straight away.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveResult(ResultFormViewModel model)
    {
        TimeSpan? time = null;
        if (!string.IsNullOrWhiteSpace(model.FinishTime))
        {
            var text = model.FinishTime.Trim();
            if (!TimePattern.IsMatch(text) || !TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
            {
                TempData["Error"] = "The finish time must look like 01:45:30 (hours:minutes:seconds).";
                return RedirectToAction(nameof(Results), new { id = model.EventId });
            }
            time = parsed;
        }

        var input = new ResultInput(time, model.Position, model.Status);
        var result = model.ResultId is > 0
            ? await _api.UpdateResultAsync(model.ResultId.Value, input)
            : await _api.CaptureResultAsync(model.EnrolmentId, input);

        if (GlobalFailure(result) is { } early) return early;

        if (result.Success) TempData["Success"] = $"Result saved for {result.Data!.ParticipantName}.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Results), new { id = model.EventId });
    }

    // ================= Helpers =================

    private async Task<(IActionResult? Failure, CategoriesViewModel? Page)> BuildCategoriesAsync(int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);
        if (GlobalFailure(ev) is { } early) return (early, null);

        if (!ev.Success)
        {
            TempData["Error"] = ev.Error;
            return (RedirectToAction(nameof(Events)), null);
        }

        if (ev.Data!.OrganiserId != CurrentUser!.UserId)
            return (RedirectToAction("AccessDenied", "Account"), null);

        var categories = await _api.GetCategoriesAsync(eventId);
        if (GlobalFailure(categories) is { } categoryFailure) return (categoryFailure, null);

        if (!categories.Success) ViewBag.Error = categories.Error;
        return (null, new CategoriesViewModel
        {
            Event = ev.Data,
            Categories = categories.Data ?? new List<CategoryDto>(),
            NewCategory = new CategoryFormViewModel { EventId = eventId }
        });
    }

    private static EventInput ToInput(EventFormViewModel model) => new(
        model.Name.Trim(),
        string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
        model.EventDate!.Value,
        model.Location.Trim(),
        model.Distance,
        model.EventType);

    private static EventFormViewModel ToForm(EventDto ev) => new()
    {
        EventId = ev.EventId,
        Name = ev.Name,
        Description = ev.Description,
        EventDate = ev.EventDate,
        Location = ev.Location,
        Distance = ev.Distance,
        EventType = ev.EventType,
        ExistingBannerUrl = ev.BannerImageUrl
    };
}
