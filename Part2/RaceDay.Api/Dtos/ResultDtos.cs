using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class ResultRequest
{
    /// <summary>Finishing time as hh:mm:ss, for example "01:05:30". Required when Status is "Finished".</summary>
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position (1 = first).</summary>
    [Range(1, 1000000)]
    public int? Position { get; set; }

    /// <summary>One of "Finished", "DNF", "DNS", "DSQ" or "Not Started".</summary>
    [RegularExpression("^(Not Started|Finished|DNF|DNS|DSQ)$", ErrorMessage = "Status must be 'Finished', 'DNF', 'DNS', 'DSQ' or 'Not Started'.")]
    public string Status { get; set; } = "Finished";
}

public record ResultResponse(
    int ResultId, int EnrolmentId, string ParticipantName, string EventName, string CategoryName,
    TimeSpan? FinishTime, int? Position, string Status);
