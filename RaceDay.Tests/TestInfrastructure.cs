using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>
/// Starts the real API in memory ("Testing" environment) with an in-memory database,
/// so tests run the full pipeline (authentication, role checks, validation) without SQL Server.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}

/// <summary>A registered, logged-in test user. The client's cookie container holds their session cookie.</summary>
public record TestUser(HttpClient Client, UserResponse User, string Email, string Password);

/// <summary>Helper methods shared by the test classes.</summary>
public class ApiHelper
{
    /// <summary>Must match "OrganiserSignupCode" in RaceDay.Api/appsettings.json.</summary>
    public const string OrganiserCode = "RACEDAY-ORG-2026";
    public const string DefaultPassword = "Passw0rd!23";

    private readonly RaceDayFactory _factory;

    public ApiHelper(RaceDayFactory factory) => _factory = factory;

    public HttpClient CreateAnonymousClient() => _factory.CreateClient();

    /// <summary>A client that does not store cookies, so tests can send a hand-made cookie.</summary>
    public HttpClient CreateClientWithoutCookies() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.raceday.co.za";

    /// <summary>Registers a new user, logs them in (starting a session) and returns their client.</summary>
    public async Task<TestUser> RegisterAndLoginAsync(string role)
    {
        var email = UniqueEmail(role.ToLowerInvariant());
        var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = $"Test {role}",
            Email = email,
            Password = DefaultPassword,
            Role = role,
            OrganiserCode = role == "Organiser" ? OrganiserCode : null
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = DefaultPassword
        });
        login.EnsureSuccessStatusCode();

        // The API remembers this user in a server-side session; the client keeps the session cookie
        var user = (await login.Content.ReadFromJsonAsync<UserResponse>())!;

        return new TestUser(client, user, email, DefaultPassword);
    }

    public static EventRequest SampleEvent(string name = "Test 10km Road Race") => new()
    {
        Name = name,
        Description = "A test event",
        EventDate = DateTime.UtcNow.AddMonths(2),
        Location = "Johannesburg, Gauteng",
        Distance = 10m,
        EventType = "Run"
    };

    public async Task<EventResponse> CreateEventAsync(HttpClient organiser, string name = "Test 10km Road Race")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", SampleEvent(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    public async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiser, int eventId, decimal entryFee = 150m)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new CategoryRequest
        {
            Name = "10km Open",
            Distance = 10m,
            EntryFee = entryFee
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    public async Task<EnrolmentResponse> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrol", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }
}
