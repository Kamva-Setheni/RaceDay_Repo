using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Pages for logged-in Participants: my events, my results and my profile.</summary>
[SessionAuthorize(Role = "Participant")]
public class ParticipantController : BaseController
{
    private readonly RaceDayApiClient _api;

    public ParticipantController(RaceDayApiClient api) => _api = api;

    // ----- My events -----

    public async Task<IActionResult> MyEvents()
    {
        var result = await _api.GetMyEnrolmentsAsync();
        if (GlobalFailure(result) is { } early) return early;

        if (!result.Success) ViewBag.Error = result.Error;
        var enrolments = (result.Data ?? new List<EnrolmentDto>()).OrderBy(e => e.EventDate).ToList();
        return View(enrolments);
    }

    [HttpPost]
    public async Task<IActionResult> Pay(int id)
    {
        var mine = await _api.GetMyEnrolmentsAsync();
        if (GlobalFailure(mine) is { } early) return early;

        var enrolment = mine.Data?.FirstOrDefault(e => e.EnrolmentId == id);
        if (!mine.Success || enrolment is null)
        {
            TempData["Error"] = mine.Error ?? "We could not find that enrolment.";
            return RedirectToAction(nameof(MyEvents));
        }

        var payment = await _api.PayAsync(id, enrolment.EntryFee);
        if (GlobalFailure(payment) is { } failure) return failure;

        if (payment.Success) TempData["Success"] = $"Payment received for {enrolment.EventName}. Your entry is confirmed!";
        else TempData["Error"] = payment.Error;

        return RedirectToAction(nameof(MyEvents));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _api.CancelEnrolmentAsync(id);
        if (GlobalFailure(result) is { } early) return early;

        if (result.Success) TempData["Success"] = "Your enrolment has been cancelled.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(MyEvents));
    }

    // ----- My results -----

    public async Task<IActionResult> MyResults()
    {
        var result = await _api.GetMyResultsAsync();
        if (GlobalFailure(result) is { } early) return early;

        if (!result.Success) ViewBag.Error = result.Error;
        var results = (result.Data ?? new List<ResultDto>()).OrderByDescending(r => r.EventDate).ToList();
        return View(results);
    }

    // ----- Profile -----

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var result = await _api.GetMeAsync();
        if (GlobalFailure(result) is { } early) return early;

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", "Home");
        }

        return View(ToProfile(result.Data!));
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.UpdateProfileAsync(model.FullName, model.Email);
        if (GlobalFailure(result) is { } early) return early;

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We could not update your profile.");
            return View(model);
        }

        HttpContext.Session.SetString(SessionKeys.FullName, result.Data!.FullName);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            TempData["Error"] = "Please choose a picture to upload.";
            return RedirectToAction(nameof(Profile));
        }

        var result = await _api.UploadProfilePictureAsync(picture);
        if (GlobalFailure(result) is { } early) return early;

        if (result.Success)
        {
            HttpContext.Session.SetString(SessionKeys.ProfilePictureUrl, result.Data!.ProfilePictureUrl ?? string.Empty);
            TempData["Success"] = "Your profile picture has been updated.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Profile));
    }

    private static ProfileViewModel ToProfile(UserDto user) => new()
    {
        FullName = user.FullName,
        Email = user.Email,
        ProfilePictureUrl = user.ProfilePictureUrl,
        Role = user.Role
    };
}
