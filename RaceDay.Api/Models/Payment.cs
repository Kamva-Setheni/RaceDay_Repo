namespace RaceDay.Api.Models;

public class Payment
{
    public int PaymentId { get; set; }
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Status { get; set; } = "Pending";

    public Enrolment? Enrolment { get; set; }
}