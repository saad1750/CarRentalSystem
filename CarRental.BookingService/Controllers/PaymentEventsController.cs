using CarRental.BookingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.BookingService.Controllers;

// MIDLERTIDIG: Payment Service kalder denne direkte over REST for nu.
// Skift til rigtig message consumer når RabbitMQ/Kafka er sat op.
[ApiController]
[Route("api/[controller]")]
public class PaymentEventsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public PaymentEventsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public record PaymentFailedRequest(Guid BookingId);

    // Krav: fejlet betaling annullerer reservationen.
    [HttpPost("payment-failed")]
    public async Task<IActionResult> PaymentFailed(PaymentFailedRequest request)
    {
        await _bookingService.HandlePaymentFailedAsync(request.BookingId);
        return Ok();
    }
}
