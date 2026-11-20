using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Event filters, the organiser dashboard data and the results summary used by the web app.</summary>
public class EventQueryTests : IClassFixture<RaceDayFactory>
{
    private readonly ApiHelper _api;

    public EventQueryTests(RaceDayFactory factory) => _api = new ApiHelper(factory);

    private static async Task<EventResponse> CreateEventAsync(HttpClient organiser, string name, string type, DateTime date)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new EventRequest
        {
            Name = name,
            Description = "Query test event",
            EventDate = date,
            Location = "Pretoria, Gauteng",
            Distance = 10m,
            EventType = type
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    [Fact]
    public async Task Filter_ByEventType_ReturnsOnlyEventsOfThatType()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var tag = Guid.NewGuid().ToString("N");
        var future = DateTime.Today.AddDays(30);
        await CreateEventAsync(organiser.Client, $"Run-{tag}", "Run", future);
        await CreateEventAsync(organiser.Client, $"Walk-{tag}", "Walk", future);

        var list = await _api.CreateAnonymousClient()
            .GetFromJsonAsync<List<EventResponse>>($"/api/events?eventType=Walk&search={tag}");

        var only = Assert.Single(list!);
        Assert.Equal("Walk", only.EventType);
    }

    [Fact]
    public async Task Filter_UpcomingOnly_HidesEventsThatAlreadyHappened()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var tag = Guid.NewGuid().ToString("N");
        await CreateEventAsync(organiser.Client, $"Past-{tag}", "Run", new DateTime(2020, 1, 1));
        await CreateEventAsync(organiser.Client, $"Future-{tag}", "Run", DateTime.Today.AddDays(30));

        var all = await _api.CreateAnonymousClient().GetFromJsonAsync<List<EventResponse>>($"/api/events?search={tag}");
        var upcoming = await _api.CreateAnonymousClient().GetFromJsonAsync<List<EventResponse>>($"/api/events?upcomingOnly=true&search={tag}");

        Assert.Equal(2, all!.Count);
        var only = Assert.Single(upcoming!);
        Assert.StartsWith("Future-", only.Name);
    }

    [Fact]
    public async Task Filter_BySearchText_MatchesTheEventName()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var tag = Guid.NewGuid().ToString("N");
        await CreateEventAsync(organiser.Client, $"Sunrise-{tag}", "Run", DateTime.Today.AddDays(10));
        await CreateEventAsync(organiser.Client, $"Sunset-{tag}", "Run", DateTime.Today.AddDays(10));

        var list = await _api.CreateAnonymousClient().GetFromJsonAsync<List<EventResponse>>($"/api/events?search=Sunrise-{tag}");

        var only = Assert.Single(list!);
        Assert.Equal($"Sunrise-{tag}", only.Name);
    }

    [Fact]
    public async Task Filter_WithAnInvalidEventType_ReturnsBadRequest()
    {
        var response = await _api.CreateAnonymousClient().GetAsync("/api/events?eventType=Swim");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrganiserDashboard_ShowsOnlyOwnEvents_WithEnrolmentCounts()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var other = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var mine = await _api.CreateEventAsync(organiser.Client, "Dashboard Event");
        await _api.CreateEventAsync(other.Client, "Someone Else's Event");
        var category = await _api.CreateCategoryAsync(organiser.Client, mine.EventId);
        await _api.EnrolAsync(participant.Client, category.CategoryId);

        var summary = await organiser.Client.GetFromJsonAsync<List<OrganiserEventSummary>>("/api/events/mine");

        var row = Assert.Single(summary!);
        Assert.Equal(mine.EventId, row.EventId);
        Assert.Equal(1, row.CategoryCount);
        Assert.Equal(1, row.EnrolmentCount);
    }

    [Fact]
    public async Task OrganiserDashboard_IsRejectedForParticipantsAndAnonymousUsers()
    {
        var participant = await _api.RegisterAndLoginAsync("Participant");

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.Client.GetAsync("/api/events/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateAnonymousClient().GetAsync("/api/events/mine")).StatusCode);
    }

    [Fact]
    public async Task MyResults_IncludeTheEventDate_AndHowManyPeopleFinished()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var first = await _api.RegisterAndLoginAsync("Participant");
        var second = await _api.RegisterAndLoginAsync("Participant");
        var third = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var e1 = await _api.EnrolAsync(first.Client, category.CategoryId);
        var e2 = await _api.EnrolAsync(second.Client, category.CategoryId);
        var e3 = await _api.EnrolAsync(third.Client, category.CategoryId);

        (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{e1.EnrolmentId}/results",
            new ResultRequest { FinishTime = TimeSpan.FromMinutes(40), Position = 1, Status = "Finished" })).EnsureSuccessStatusCode();
        (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{e2.EnrolmentId}/results",
            new ResultRequest { FinishTime = TimeSpan.FromMinutes(45), Position = 2, Status = "Finished" })).EnsureSuccessStatusCode();
        (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{e3.EnrolmentId}/results",
            new ResultRequest { Status = "DNF" })).EnsureSuccessStatusCode();

        var results = await second.Client.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");

        var result = Assert.Single(results!);
        Assert.Equal(2, result.Position);
        Assert.Equal(2, result.TotalFinishers); // the DNF is not counted as a finisher
        Assert.Equal(ev.EventId, result.EventId);
        Assert.Equal(ev.EventDate.Date, result.EventDate.Date);
    }
}
