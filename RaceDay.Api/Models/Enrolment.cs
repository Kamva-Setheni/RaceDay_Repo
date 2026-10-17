namespace RaceDay.Api.Models;

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = "Pending";

    public User? Participant { get; set; }
    public Category? Category { get; set; }
    public Result? Result { get; set; }
    public Payment? Payment { get; set; }
}