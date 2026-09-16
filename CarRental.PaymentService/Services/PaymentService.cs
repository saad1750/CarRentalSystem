using System.Collections.Concurrent;
using CarRental.PaymentService.Models;
using CarRental.Shared.Enums;

namespace CarRental.PaymentService.Services;

// NB: gemmer i memory for nu — skiftes til EF Core-database senere.
public class PaymentService : IPaymentService
{
    private static readonly ConcurrentDictionary<Guid, Payment> Payments = new();

    private readonly IBookingServiceClient _bookingServiceClient;

    public PaymentService(IBookingServiceClient bookingServiceClient)
    {
        _bookingServiceClient = bookingServiceClient;
    }

    // Krav: betaling online. Idempotent — samme booking betales aldrig to gange.
    public async Task<Payment> ProcessPaymentAsync(Guid bookingId, decimal amount)
    {
        var existing = Payments.Values.FirstOrDefault(p => p.BookingId == bookingId
                                                             && p.Status == PaymentStatus.Succeeded);
        if (existing is not null)
            return existing;

        var payment = new Payment
        {
            BookingId = bookingId,
            Amount = amount
        };

        // Simuleret gateway-kald — skift til rigtig udbyder (Stripe/PayPal) senere.
        var success = SimulateGatewayCall();

        payment.ProcessedAt = DateTime.UtcNow;
        payment.Status = success ? PaymentStatus.Succeeded : PaymentStatus.Failed;
        if (!success)
            payment.FailureReason = "Simulated gateway decline.";

        Payments[payment.Id] = payment;

        // Krav: fejlet betaling annullerer bookingen.
        if (success)
            await _bookingServiceClient.ConfirmBookingAsync(bookingId);
        else
            await _bookingServiceClient.NotifyPaymentFailedAsync(bookingId);

        return payment;
    }

    public Task<Payment?> GetByBookingIdAsync(Guid bookingId)
    {
        var payment = Payments.Values
            .Where(p => p.BookingId == bookingId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(payment);
    }

    // Simuleret — lykkes altid for nu.
    private static bool SimulateGatewayCall() => true;
}
