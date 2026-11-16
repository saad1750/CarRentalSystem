using CarRental.BookingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.BookingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    // Sat op via DI i Program.cs.
    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public record CreateBookingRequest(int CustomerId, int CarId, DateTime StartDate, DateTime EndDate);

    // Krav: booking af bil for en valgt periode.
    [HttpPost]
    public async Task<IActionResult> CreateBooking(CreateBookingRequest request)
    {
        var result = await _bookingService.CreateBookingAsync(
            request.CustomerId, request.CarId, request.StartDate, request.EndDate);

        return result.Success ? Ok(result.Booking) : BadRequest(result.Error);
    }

    // Krav: annullering af booking.
    [HttpPost("{bookingId:int}/cancel")]
    public async Task<IActionResult> CancelBooking(int bookingId)
    {
        var result = await _bookingService.CancelBookingAsync(bookingId);
        return result.Success ? Ok(result.Booking) : BadRequest(result.Error);
    }

    // Kaldes af Payment Service efter vellykket betaling.
    [HttpPost("{bookingId:int}/confirm")]
    public async Task<IActionResult> ConfirmBooking(int bookingId)
    {
        var result = await _bookingService.ConfirmBookingAsync(bookingId);
        return result.Success ? Ok(result.Booking) : BadRequest(result.Error);
    }

    // Krav: booking-historik.
    [HttpGet("history/{customerId:int}")]
    public async Task<IActionResult> GetHistory(int customerId)
    {
        var history = await _bookingService.GetBookingHistoryAsync(customerId);
        return Ok(history);
    }

    // Personale: bekræft afhentning.
    [HttpPost("{bookingId:int}/pickup")]
    public async Task<IActionResult> ConfirmPickup(int bookingId)
    {
        var result = await _bookingService.ConfirmPickupAsync(bookingId);
        return result.Success ? Ok(result.Booking) : BadRequest(result.Error);
    }

    // Personale: registrer aflevering.
    [HttpPost("{bookingId:int}/return")]
    public async Task<IActionResult> RegisterReturn(int bookingId)
    {
        var result = await _bookingService.RegisterReturnAsync(bookingId);
        return result.Success ? Ok(result.Booking) : BadRequest(result.Error);
    }
}
