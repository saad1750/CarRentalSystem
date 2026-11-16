using CarRental.PaymentService.Models;

namespace CarRental.PaymentService.Services;

public interface IPaymentService
{
    // Krav: betaling online, fejlet betaling annullerer bookingen.
    Task<Payment> ProcessPaymentAsync(int bookingId, decimal amount);

    Task<Payment?> GetByBookingIdAsync(int bookingId);
}
