using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class EventRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Required]
    public DateTime? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Overall event distance in kilometres.</summary>
    [Range(typeof(decimal), "0.01", "9999.99")]
    public decimal Distance { get; set; }

    /// <summary>One of "Run", "Walk" or "Cycle".</summary>
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be 'Run', 'Walk' or 'Cycle'.")]
    public string EventType { get; set; } = string.Empty;
}

public record EventResponse(
    int EventId, int OrganiserId, string Name, string? Description,
    DateTime EventDate, string Location, decimal Distance, string EventType, DateTime CreatedAt,
    string? BannerImageUrl = null);

/// <summary>One row of the Organiser dashboard: an event with its enrolment and category counts.</summary>
public record OrganiserEventSummary(
    int EventId, string Name, DateTime EventDate, string Location, string EventType,
    decimal Distance, string? BannerImageUrl, int CategoryCount, int EnrolmentCount);
