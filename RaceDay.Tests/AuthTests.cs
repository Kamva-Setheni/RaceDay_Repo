using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>User registration and login (success and failure scenarios).</summary>
public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    private readonly ApiHelper _api;

    public AuthTests(RaceDayFactory factory)
    {
        _factory = factory;
        _api = new ApiHelper(factory);
    }

    private static RegisterRequest NewParticipant(string? email = null) => new()
    {
        FullName = "Thabo Mokoena",
        Email = email ?? ApiHelper.UniqueEmail("thabo"),
        Password = ApiHelper.DefaultPassword,
        Role = "Participant"
    };

    [Fact]
    public async Task Register_Participant_ReturnsCreated_WithoutExposingPassword()
    {
        var client = _api.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewParticipant());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Participant", user!.Role);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_ReturnsCreated()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        request.Role = "Organiser";
        request.OrganiserCode = ApiHelper.OrganiserCode;

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_ReturnsForbidden()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        request.Role = "Organiser";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_ReturnsForbidden()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        request.Role = "Organiser";
        request.OrganiserCode = "NOT-THE-CODE";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant("not-an-email");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        request.Password = "short";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_ReturnsBadRequest()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        request.Role = "Admin";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresPasswordAsBcryptHash_NotPlainText()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Email == request.Email);

        Assert.NotEqual(request.Password, stored.PasswordHash);
        Assert.StartsWith("$2", stored.PasswordHash); // BCrypt hashes start with $2
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsUser_AndStartsASession()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = request.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Participant", user!.Role);

        // The session now remembers the user, so a protected endpoint works without logging in again
        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_SetsAnHttpOnlySessionCookie()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = request.Password });

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, c =>
            c.StartsWith(".RaceDay.Session=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Logout_EndsTheSession_SoProtectedEndpointsReturnUnauthorized()
    {
        var participant = await _api.RegisterAndLoginAsync("Participant");
        Assert.Equal(HttpStatusCode.OK, (await participant.Client.GetAsync("/api/users/me")).StatusCode);

        var logout = await participant.Client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await participant.Client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutBeingLoggedIn_ReturnsUnauthorized()
    {
        var response = await _api.CreateAnonymousClient().PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = _api.CreateAnonymousClient();
        var request = NewParticipant();
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = "WrongPassword!1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var client = _api.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = ApiHelper.UniqueEmail("nobody"), Password = ApiHelper.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutLoggingIn_ReturnsUnauthorized()
    {
        var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WhenLoggedIn_ReturnsOwnDetails_AndCanBeUpdated()
    {
        var participant = await _api.RegisterAndLoginAsync("Participant");

        var get = await participant.Client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var me = await get.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(participant.Email, me!.Email);

        var put = await participant.Client.PutAsJsonAsync("/api/users/me",
            new UpdateProfileRequest { FullName = "Updated Name", Email = participant.Email });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Updated Name", updated!.FullName);
    }
}
