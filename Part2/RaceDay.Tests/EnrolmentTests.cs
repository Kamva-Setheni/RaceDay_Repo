using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Event enrolments, results and payments.</summary>
public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly ApiHelper _api;

    public EnrolmentTests(RaceDayFactory factory) => _api = new ApiHelper(factory);

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecordedWithParticipantEventAndCategory()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await participant.Client.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal(participant.User.UserId, enrolment!.ParticipantId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(ev.EventId, enrolment.EventId);

        var mine = await participant.Client.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments");
        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId);
    }

    [Fact]
    public async Task Participant_EnrollingTwiceInTheSameCategory_ReturnsConflict()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        await _api.EnrolAsync(participant.Client, category.CategoryId);

        var second = await participant.Client.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_InCategoryThatDoesNotExist_ReturnsNotFound()
    {
        var participant = await _api.RegisterAndLoginAsync("Participant");

        var response = await participant.Client.PostAsync("/api/categories/999999/enrol", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);

        var response = await organiser.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
        Assert.Contains(list!, e => e.EnrolmentId == enrolment.EnrolmentId);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _api.RegisterAndLoginAsync("Organiser");
        var other = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(owner.Client);

        var response = await other.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);

        var response = await participant.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var mine = await participant.Client.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments");
        Assert.DoesNotContain(mine!, e => e.EnrolmentId == enrolment.EnrolmentId);
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var owner = await _api.RegisterAndLoginAsync("Participant");
        var other = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await _api.EnrolAsync(owner.Client, category.CategoryId);

        var response = await other.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanViewTheirOwnResult()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);

        var capture = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { FinishTime = new TimeSpan(0, 52, 14), Position = 3, Status = "Finished" });

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var mine = await participant.Client.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");
        var result = Assert.Single(mine!);
        Assert.Equal(3, result.Position);
        Assert.Equal(new TimeSpan(0, 52, 14), result.FinishTime);
    }

    [Fact]
    public async Task CaptureResult_Twice_ReturnsConflict()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);
        var request = new ResultRequest { FinishTime = TimeSpan.FromMinutes(50), Position = 1, Status = "Finished" };
        (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", request)).EnsureSuccessStatusCode();

        var second = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutTime_ReturnsBadRequest()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new ResultRequest { Position = 1, Status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanPayEntryFee_AndEnrolmentBecomesConfirmed()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId, entryFee: 250m);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);

        var pay = await participant.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/payment",
            new PaymentRequest { Amount = 250m });

        Assert.Equal(HttpStatusCode.Created, pay.StatusCode);
        var mine = await participant.Client.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments");
        Assert.Equal("Confirmed", mine!.Single(e => e.EnrolmentId == enrolment.EnrolmentId).Status);
    }

    [Fact]
    public async Task Pay_WrongAmount_ReturnsBadRequest_AndPayingTwiceReturnsConflict()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var category = await _api.CreateCategoryAsync(organiser.Client, ev.EventId, entryFee: 250m);
        var enrolment = await _api.EnrolAsync(participant.Client, category.CategoryId);
        var url = $"/api/enrolments/{enrolment.EnrolmentId}/payment";

        var wrong = await participant.Client.PostAsJsonAsync(url, new PaymentRequest { Amount = 10m });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        (await participant.Client.PostAsJsonAsync(url, new PaymentRequest { Amount = 250m })).EnsureSuccessStatusCode();
        var again = await participant.Client.PostAsJsonAsync(url, new PaymentRequest { Amount = 250m });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }
}
