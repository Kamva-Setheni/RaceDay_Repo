using System.Net;
using System.Net.Http.Json;
using System.Text;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Event banners and profile pictures: uploads go through the API to (fake) Azure Blob Storage.</summary>
public class ImageUploadTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    private readonly ApiHelper _api;

    public ImageUploadTests(RaceDayFactory factory)
    {
        _factory = factory;
        _api = new ApiHelper(factory);
    }

    private static string BannerUrl(int eventId) => $"/api/events/{eventId}/banner";

    [Fact]
    public async Task Organiser_CanUploadBanner_AndTheUrlIsSavedOnTheEvent()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync(BannerUrl(ev.EventId), ApiHelper.ImageForm(ApiHelper.TinyPng, "banner.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.StartsWith("https://fake-storage.test/event-banners/", updated!.BannerImageUrl);

        // the URL is stored in the database, so a fresh public request returns it too
        var fetched = await _api.CreateAnonymousClient().GetFromJsonAsync<EventResponse>($"/api/events/{ev.EventId}");
        Assert.Equal(updated.BannerImageUrl, fetched!.BannerImageUrl);
    }

    [Fact]
    public async Task ReplacingTheBanner_DeletesTheOldImage()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var first = await (await organiser.Client.PostAsync(BannerUrl(ev.EventId), ApiHelper.ImageForm(ApiHelper.TinyPng, "one.png")))
            .Content.ReadFromJsonAsync<EventResponse>();

        var secondResponse = await organiser.Client.PostAsync(BannerUrl(ev.EventId), ApiHelper.ImageForm(ApiHelper.TinyPng, "two.png"));
        var second = await secondResponse.Content.ReadFromJsonAsync<EventResponse>();

        Assert.NotEqual(first!.BannerImageUrl, second!.BannerImageUrl);
        Assert.Contains(first.BannerImageUrl!, _factory.Blobs.Deleted);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_ReturnsForbidden()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var participant = await _api.RegisterAndLoginAsync("Participant");
        var ev = await _api.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsync(BannerUrl(ev.EventId), ApiHelper.ImageForm(ApiHelper.TinyPng, "banner.png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _api.RegisterAndLoginAsync("Organiser");
        var other = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(owner.Client);

        var response = await other.Client.PostAsync(BannerUrl(ev.EventId), ApiHelper.ImageForm(ApiHelper.TinyPng, "banner.png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotUploadBanner_ReturnsUnauthorized()
    {
        var response = await _api.CreateAnonymousClient()
            .PostAsync(BannerUrl(1), ApiHelper.ImageForm(ApiHelper.TinyPng, "banner.png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Banner_ForAnEventThatDoesNotExist_ReturnsNotFound()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");

        var response = await organiser.Client.PostAsync(BannerUrl(999999), ApiHelper.ImageForm(ApiHelper.TinyPng, "banner.png"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Banner_ThatIsReallyATextFile_ReturnsBadRequest()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var notAnImage = Encoding.UTF8.GetBytes("this is plain text pretending to be a picture");

        var response = await organiser.Client.PostAsync(BannerUrl(ev.EventId), ApiHelper.ImageForm(notAnImage, "fake.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Banner_WithADisallowedExtension_ReturnsBadRequest()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync(BannerUrl(ev.EventId),
            ApiHelper.ImageForm(ApiHelper.TinyPng, "notes.txt", "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Banner_LargerThanFiveMegabytes_ReturnsBadRequest()
    {
        var organiser = await _api.RegisterAndLoginAsync("Organiser");
        var ev = await _api.CreateEventAsync(organiser.Client);
        var tooBig = new byte[(5 * 1024 * 1024) + 1];
        Array.Copy(ApiHelper.TinyPng, tooBig, ApiHelper.TinyPng.Length);

        var response = await organiser.Client.PostAsync(BannerUrl(ev.EventId), ApiHelper.ImageForm(tooBig, "huge.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanUploadProfilePicture_AndItAppearsOnTheProfile()
    {
        var participant = await _api.RegisterAndLoginAsync("Participant");

        var response = await participant.Client.PostAsync("/api/users/me/profile-picture",
            ApiHelper.ImageForm(ApiHelper.TinyPng, "me.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await participant.Client.GetFromJsonAsync<UserResponse>("/api/users/me");
        Assert.StartsWith("https://fake-storage.test/profile-pictures/", me!.ProfilePictureUrl);
    }

    [Fact]
    public async Task ProfilePicture_WithoutLoggingIn_ReturnsUnauthorized()
    {
        var response = await _api.CreateAnonymousClient()
            .PostAsync("/api/users/me/profile-picture", ApiHelper.ImageForm(ApiHelper.TinyPng, "me.png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_ThatIsNotAnImage_ReturnsBadRequest()
    {
        var participant = await _api.RegisterAndLoginAsync("Participant");

        var response = await participant.Client.PostAsync("/api/users/me/profile-picture",
            ApiHelper.ImageForm(Encoding.UTF8.GetBytes("not an image at all"), "me.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
