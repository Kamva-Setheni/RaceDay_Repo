using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

/// <summary>Shared behaviour for all controllers: JSON, automatic validation (400) and error helpers.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The id of the logged-in user, taken from the session.</summary>
    protected int CurrentUserId => User.GetUserId();

    protected ObjectResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");

    protected ObjectResult UnauthorizedProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");

    protected ObjectResult ForbiddenProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");

    protected ObjectResult ServiceUnavailableProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service Unavailable");

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
}
