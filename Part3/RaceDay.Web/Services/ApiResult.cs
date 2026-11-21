namespace RaceDay.Web.Services;

/// <summary>The outcome of a call to the RaceDay API. Errors are always friendly, never raw.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }

    public bool IsUnauthorized => StatusCode == 401;
    public bool IsForbidden => StatusCode == 403;
    public bool IsNotFound => StatusCode == 404;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T? data, int statusCode) =>
        new() { Success = true, StatusCode = statusCode, Data = data };

    public static ApiResult<T> Fail(int statusCode, string error) =>
        new() { Success = false, StatusCode = statusCode, Error = error };
}

/// <summary>Marker type for API calls that return no body (204 No Content).</summary>
public sealed class NoContent { }
