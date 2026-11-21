using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// The only way the MVC application gets or changes data: every call goes to the RaceDay API.
/// The user's API session cookie (kept in their MVC session) is added to each request, so the API
/// knows who is calling and applies its own role checks. Errors are turned into friendly messages.
/// </summary>
public class RaceDayApiClient
{
    private const string SessionCookieName = ".RaceDay.Session=";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;

    public RaceDayApiClient(HttpClient http, IHttpContextAccessor accessor)
    {
        _http = http;
        _accessor = accessor;
    }

    // ================= Authentication =================

    public async Task<ApiResult<LoginResult>> LoginAsync(string email, string password)
    {
        string? cookie = null;
        var result = await SendAsync<UserDto>(HttpMethod.Post, "api/auth/login",
            JsonContent.Create(new { email, password }, options: JsonOptions),
            attachSession: false,
            onResponse: response => cookie = ExtractSessionCookie(response));

        if (!result.Success)
            return ApiResult<LoginResult>.Fail(result.StatusCode, result.Error ?? "We could not log you in.");

        if (result.Data is null || cookie is null)
            return ApiResult<LoginResult>.Fail(500, "We could not start your session. Please try again.");

        return ApiResult<LoginResult>.Ok(new LoginResult(result.Data, cookie), result.StatusCode);
    }

    public Task<ApiResult<UserDto>> RegisterAsync(string fullName, string email, string password, string role, string? organiserCode) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/register",
            JsonContent.Create(new { fullName, email, password, role, organiserCode }, options: JsonOptions),
            attachSession: false);

    public async Task<ApiResult> LogoutAsync() =>
        await SendAsync<NoContent>(HttpMethod.Post, "api/auth/logout");

    // ================= Profile =================

    public Task<ApiResult<UserDto>> GetMeAsync() =>
        SendAsync<UserDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserDto>> UpdateProfileAsync(string fullName, string email) =>
        SendAsync<UserDto>(HttpMethod.Put, "api/users/me",
            JsonContent.Create(new { fullName, email }, options: JsonOptions));

    public Task<ApiResult<UserDto>> UploadProfilePictureAsync(IFormFile file) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/users/me/profile-picture", BuildFileContent(file));

    // ================= Events =================

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(string? search, string? eventType, bool upcomingOnly, DateTime? from, DateTime? to)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(eventType)) query.Add($"eventType={Uri.EscapeDataString(eventType)}");
        if (upcomingOnly) query.Add("upcomingOnly=true");
        if (from.HasValue) query.Add($"from={Iso(from.Value.Date)}");
        if (to.HasValue) query.Add($"to={Iso(to.Value.Date.AddDays(1).AddSeconds(-1))}"); // include the whole day

        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return SendAsync<List<EventDto>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventDto>> GetEventAsync(int id) =>
        SendAsync<EventDto>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<List<OrganiserEventDto>>> GetMyEventsAsync() =>
        SendAsync<List<OrganiserEventDto>>(HttpMethod.Get, "api/events/mine");

    public Task<ApiResult<EventDto>> CreateEventAsync(EventInput input) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events", JsonContent.Create(input, options: JsonOptions));

    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventInput input) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", JsonContent.Create(input, options: JsonOptions));

    public async Task<ApiResult> DeleteEventAsync(int id) =>
        await SendAsync<NoContent>(HttpMethod.Delete, $"api/events/{id}");

    public Task<ApiResult<EventDto>> UploadBannerAsync(int id, IFormFile file) =>
        SendAsync<EventDto>(HttpMethod.Post, $"api/events/{id}/banner", BuildFileContent(file));

    // ================= Categories =================

    public Task<ApiResult<List<CategoryDto>>> GetCategoriesAsync(int eventId) =>
        SendAsync<List<CategoryDto>>(HttpMethod.Get, $"api/events/{eventId}/categories");

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryInput input) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", JsonContent.Create(input, options: JsonOptions));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int id, CategoryInput input) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{id}", JsonContent.Create(input, options: JsonOptions));

    public async Task<ApiResult> DeleteCategoryAsync(int id) =>
        await SendAsync<NoContent>(HttpMethod.Delete, $"api/categories/{id}");

    // ================= Enrolments =================

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, $"api/categories/{categoryId}/enrol");

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/users/me/enrolments");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public async Task<ApiResult> CancelEnrolmentAsync(int id) =>
        await SendAsync<NoContent>(HttpMethod.Delete, $"api/enrolments/{id}");

    // ================= Results =================

    public Task<ApiResult<ResultDto>> CaptureResultAsync(int enrolmentId, ResultInput input) =>
        SendAsync<ResultDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/results", JsonContent.Create(input, options: JsonOptions));

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultInput input) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}", JsonContent.Create(input, options: JsonOptions));

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, "api/users/me/results");

    public Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId) =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/events/{eventId}/results");

    // ================= Payments =================

    public Task<ApiResult<PaymentDto>> PayAsync(int enrolmentId, decimal amount) =>
        SendAsync<PaymentDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/payment",
            JsonContent.Create(new { amount }, options: JsonOptions));

    // ================= Plumbing =================

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string url, HttpContent? content = null,
        bool attachSession = true, Action<HttpResponseMessage>? onResponse = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };
            if (attachSession) AttachSessionCookie(request);

            using var response = await _http.SendAsync(request);
            onResponse?.Invoke(response);

            if (response.IsSuccessStatusCode)
            {
                T? data = default;
                if (response.StatusCode != HttpStatusCode.NoContent && typeof(T) != typeof(NoContent))
                {
                    data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
                }
                return ApiResult<T>.Ok(data, (int)response.StatusCode);
            }

            return ApiResult<T>.Fail((int)response.StatusCode, await ReadErrorAsync(response));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return ApiResult<T>.Fail(0, "The RaceDay service is not reachable right now. Please try again in a moment.");
        }
    }

    /// <summary>Adds this user's API session cookie, so the API knows who is calling.</summary>
    private void AttachSessionCookie(HttpRequestMessage request)
    {
        var cookie = _accessor.HttpContext?.Session.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
    }

    /// <summary>Picks the API's session cookie ("name=value") out of a login response.</summary>
    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;

        foreach (var value in values)
        {
            var pair = value.Split(';', 2)[0].Trim();
            if (pair.StartsWith(SessionCookieName, StringComparison.Ordinal)) return pair;
        }
        return null;
    }

    private static MultipartFormDataContent BuildFileContent(IFormFile file)
    {
        var form = new MultipartFormDataContent();
        var stream = new StreamContent(file.OpenReadStream());
        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            stream.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        }
        form.Add(stream, "file", file.FileName);
        return form;
    }

    private static string Iso(DateTime date) =>
        date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Turns an API error into a message that is safe and friendly to show a user.</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;

        // Server errors can contain technical details, so they are never shown
        if (status >= 500 && status != 503) return DefaultMessage(status);

        try
        {
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(property => property.Value.ValueKind == JsonValueKind.Array
                        ? property.Value.EnumerateArray().Select(item => item.GetString())
                        : Enumerable.Empty<string?>())
                    .Where(message => !string.IsNullOrWhiteSpace(message));

                var joined = string.Join(" ", messages);
                if (joined.Length > 0) return joined;
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                var text = detail.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        catch (JsonException)
        {
            // fall through to the default message
        }

        return DefaultMessage(status);
    }

    private static string DefaultMessage(int status) => status switch
    {
        400 => "Some of the details are not valid. Please check them and try again.",
        401 => "Please log in to continue.",
        403 => "You do not have permission to do that.",
        404 => "We could not find what you were looking for.",
        409 => "That conflicts with something that already exists.",
        503 => "Image storage is not available right now. Please try again later.",
        _ => "Something went wrong on our side. Please try again."
    };
}
