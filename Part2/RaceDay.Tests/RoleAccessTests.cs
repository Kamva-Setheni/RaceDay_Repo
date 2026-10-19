using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Role-based access: the wrong role is rejected (403), not being logged in is rejected (401).</summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly ApiHelper _api;

    public RoleAccessTests(RaceDayFactory factory) => _api = new ApiHelper(factory);

    [Fact]
    public async Task Participant_CannotAddCategory_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { Name = "Sneaky", Distance = 5m, EntryFee = 0m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);

        var response = await participant.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);

        var response = await participant.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = TimeSpan.FromMinutes(45), Position = 1, Status = "Finished" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await organiser.Client.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantOnlyEndpoints_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");

        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.Client.GetAsync("/api/users/me/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.Client.GetAsync("/api/users/me/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.Client.GetAsync("/api/users/me/payments")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_IsRejectedFromEveryProtectedArea()
    {
        var anonymous = _api.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/me/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/me/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/categories/1/enrol", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/events/1/enrolments")).StatusCode);
    }

    [Fact]
    public async Task ForgedSessionCookie_IsRejected_ReturnsUnauthorized()
    {
        var client = _api.CreateClientWithoutCookies();
        client.DefaultRequestHeaders.Add("Cookie", ".RaceDay.Session=this-is-a-forged-value");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EachUserHasTheirOwnSession_WithTheirOwnRole()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");

        var organiserMe = await organiser.Client.GetFromJsonAsync<UserResponse>("/api/users/me");
        var participantMe = await participant.Client.GetFromJsonAsync<UserResponse>("/api/users/me");

        Assert.Equal("Organiser", organiserMe!.Role);
        Assert.Equal("Participant", participantMe!.Role);
        Assert.NotEqual(organiserMe.UserId, participantMe.UserId);
    }
}
