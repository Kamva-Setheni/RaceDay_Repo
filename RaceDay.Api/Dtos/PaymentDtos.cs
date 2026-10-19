using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class PaymentRequest
{
    /// <summary>Must equal the entry fee of the category the participant enrolled in.</summary>
    [Range(typeof(decimal), "0.01", "99999.99")]
    public decimal Amount { get; set; }
}

public record PaymentResponse(int PaymentId, int EnrolmentId, decimal Amount, DateTime PaymentDate, string Status);
