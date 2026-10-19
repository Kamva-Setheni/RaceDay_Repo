using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Entry-fee payments for enrolments (Participants only).</summary>
[Authorize(Roles = AppRoles.Participant)]
public class PaymentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public PaymentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Records payment of the entry fee for one of the Participant's enrolments.</summary>
    /// <remarks>The amount must equal the category's entry fee. A successful payment confirms the enrolment.</remarks>
    /// <param name="id">The enrolment id.</param>
    /// <response code="201">The payment record.</response>
    /// <response code="400">Invalid data, or the amount does not equal the entry fee.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or not the owner of this enrolment.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">This enrolment has already been paid.</response>
    [HttpPost("api/enrolments/{id:int}/payment")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Pay(int id, PaymentRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category)
            .Include(e => e.Payment)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return NotFoundProblem($"Enrolment {id} does not exist.");
        if (enrolment.ParticipantId != CurrentUserId) return ForbiddenProblem("You can only pay for your own enrolments.");
        if (enrolment.Payment is not null) return ConflictProblem("This enrolment has already been paid.");

        var fee = enrolment.Category!.EntryFee;
        if (request.Amount != fee) return BadRequestProblem($"The amount must equal the entry fee of {fee:0.00}.");

        var payment = new Payment
        {
            EnrolmentId = id,
            Amount = request.Amount,
            PaymentDate = DateTime.UtcNow,
            Status = "Paid"
        };

        enrolment.Status = "Confirmed";
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return Created("/api/users/me/payments", ToResponse(payment));
    }

    /// <summary>Returns the logged-in Participant's payment history.</summary>
    /// <response code="200">Array of payments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The user is not a Participant.</response>
    [HttpGet("api/users/me/payments")]
    [ProducesResponseType(typeof(IEnumerable<PaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<PaymentResponse>>> GetMine()
    {
        var userId = CurrentUserId;
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Enrolment!.ParticipantId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        return Ok(payments.Select(ToResponse));
    }

    private static PaymentResponse ToResponse(Payment p) =>
        new(p.PaymentId, p.EnrolmentId, p.Amount, p.PaymentDate, p.Status);
}
