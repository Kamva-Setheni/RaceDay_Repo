using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Event management: Organisers can manage events, others cannot.</summary>
public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly ApiHelper _api;

    public EventTests(RaceDayFactory factory) => _api = new ApiHelper(factory);

    [Fact]
    public async Task Organiser_CanCreateEvent_AndItAppearsInTheList()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", ApiHelper.SampleEvent("Soweto Test Run"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal(organiser.User.UserId, created!.OrganiserId);
        Assert.Equal("Run", created.EventType);
        Assert.Equal(10m, created.Distance);

        var list = await _api.CreateAnonymousClient().GetFromJsonAsync<List<EventResponse>>("/api/events");
        Assert.Contains(list!, e => e.EventId == created.EventId);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_ReturnsForbidden()
    {
        var participant = await _api.RegisterAndLoginAsync("Participant");

        var response = await participant.Client.PostAsJsonAsync("/api/events", ApiHelper.SampleEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateEvent_ReturnsUnauthorized()
    {
        var response = await _api.CreateAnonymousClient().PostAsJsonAsync("/api/events", ApiHelper.SampleEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_ReturnsBadRequest()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var request = ApiHelper.SampleEvent();
        request.EventType = "Swim";

        var response = await organiser.Client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingName_ReturnsBadRequest()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var request = ApiHelper.SampleEvent();
        request.Name = "";

        var response = await organiser.Client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CanViewEvents_AndASingleEvent()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var anonymous = _api.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task GetEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var response = await _api.CreateAnonymousClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var update = ApiHelper.SampleEvent("Renamed Event");
        update.EventType = "Walk";

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("Renamed Event", updated!.Name);
        Assert.Equal("Walk", updated.EventType);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _api.RegisterAndLoginAsync("Organiser");
        var other = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(owner.Client);

        var response = await other.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", ApiHelper.SampleEvent("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");

        var response = await organiser.Client.PutAsJsonAsync("/api/events/999999", ApiHelper.SampleEvent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_AndItIsGone()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        await _api.CreateCategoryAsync(organiser.Client, ev.EventId);

        var delete = await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var get = await _api.CreateAnonymousClient().GetAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _api.RegisterAndLoginAsync("Organiser");
        var other = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(owner.Client);

        var response = await other.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotDeleteEvent_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);

        var response = await participant.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanAddCategory_AndEveryoneCanViewIt()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);

        var list = await _api.CreateAnonymousClient()
            .GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");

        Assert.Contains(list!, c => c.CategoryId == category.CategoryId);
    }
}
