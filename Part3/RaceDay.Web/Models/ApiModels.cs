namespace RaceDay.Web.Models;

// These classes only describe the JSON the API sends and receives.
// All rules and data live in the API; the MVC project holds no business logic.

public record UserDto(int UserId, string FullName, string Email, string Role, DateTime CreatedAt, string? ProfilePictureUrl);

public record LoginResult(UserDto User, string ApiCookie);

public record EventDto(
    int EventId, int OrganiserId, string Name, string? Description, DateTime EventDate,
    string Location, decimal Distance, string EventType, DateTime CreatedAt, string? BannerImageUrl);

public record OrganiserEventDto(
    int EventId, string Name, DateTime EventDate, string Location, string EventType,
    decimal Distance, string? BannerImageUrl, int CategoryCount, int EnrolmentCount);

public record CategoryDto(int CategoryId, int EventId, string Name, decimal Distance, decimal EntryFee);

public record EnrolmentDto(
    int EnrolmentId, int ParticipantId, string ParticipantName, int CategoryId, string CategoryName,
    int EventId, string EventName, DateTime EnrolmentDate, string Status, decimal EntryFee, DateTime EventDate);

public record ResultDto(
    int ResultId, int EnrolmentId, string ParticipantName, string EventName, string CategoryName,
    TimeSpan? FinishTime, int? Position, string Status, int EventId, DateTime EventDate, int TotalFinishers);

public record PaymentDto(int PaymentId, int EnrolmentId, decimal Amount, DateTime PaymentDate, string Status);

// ----- request bodies -----
public record EventInput(string Name, string? Description, DateTime EventDate, string Location, decimal Distance, string EventType);

public record CategoryInput(string Name, decimal Distance, decimal EntryFee);

public record ResultInput(TimeSpan? FinishTime, int? Position, string Status);
