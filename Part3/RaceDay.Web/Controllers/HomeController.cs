using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : BaseController
{
    private readonly RaceDayApiClient _api;

    public HomeController(RaceDayApiClient api) => _api = api;

    /// <summary>The home page: upcoming events with filters. Organisers go straight to their dashboard.</summary>
    public async Task<IActionResult> Index(string? search, string? eventType, DateTime? from, DateTime? to)
    {
        if (CurrentUser?.IsOrganiser == true) return RedirectToAction("Dashboard", "Organiser");

        var result = await _api.GetEventsAsync(search, eventType, upcomingOnly: true, from, to);
        var model = new HomeViewModel { Search = search, EventType = eventType, From = from, To = to };

        if (result.Success) model.Events = result.Data ?? new List<EventDto>();
        else model.Error = result.Error;

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    public IActionResult StatusCodePage(int code) => View(code);
}
