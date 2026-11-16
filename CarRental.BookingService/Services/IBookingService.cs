using CarRental.BookingService.Models;

namespace CarRental.BookingService.Services;

public interface IBookingService
{
    // Krav: booking af bil for en valgt periode.
    Task<BookingResult> CreateBookingAsync(int customerId, int carId, DateTime startDate, DateTime endDate);

    // Krav: annullering af booking.
    Task<BookingResult> CancelBookingAsync(int bookingId);

    // Krav: tjek om booking allerede findes.
    Task<bool> HasOverlappingBookingAsync(int customerId, int carId, DateTime startDate, DateTime endDate);

    // Krav: se booking-historik.
    Task<List<Booking>> GetBookingHistoryAsync(int customerId);

    // Kaldes efter Payment Service bekræfter betaling.
    Task<BookingResult> ConfirmBookingAsync(int bookingId);

    // Kaldes hvis Payment Service melder fejlet betaling.
    Task HandlePaymentFailedAsync(int bookingId);

    // Personale-funktioner.
    Task<BookingResult> ConfirmPickupAsync(int bookingId);
    Task<BookingResult> RegisterReturnAsync(int bookingId);
}

// Generisk resultat fra alle BookingService-metoder.
public record BookingResult(bool Success, string? Error, Booking? Booking);
