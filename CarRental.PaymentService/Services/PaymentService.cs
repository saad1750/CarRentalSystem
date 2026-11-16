using CarRental.PaymentService.Data;
using CarRental.PaymentService.Models;
using CarRental.Shared.Enums;
using CarRental.Shared.Events;
using Microsoft.EntityFrameworkCore;

namespace CarRental.PaymentService.Services;

// Gemmer betalinger i PostgreSQL via EF Core (PaymentDbContext).
public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _db;
    private readonly IPaymentEventPublisher _eventPublisher;

    public PaymentService(PaymentDbContext db, IPaymentEventPublisher eventPublisher)
    {
        _db = db;
        _eventPublisher = eventPublisher;
    }

    // Krav: betaling online. Idempotent — samme booking betales aldrig to gange.
    public async Task<Payment> ProcessPaymentAsync(int bookingId, decimal amount)
    {
        var existing = await FindSucceededAsync(bookingId);
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

        _db.Payments.Add(payment);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (success)
        {
            // En samtidig forespørgsel nåede at gemme en vellykket betaling for samme
            // booking først (unikt index). Returnér den i stedet for at lave en dublet.
            _db.Entry(payment).State = EntityState.Detached;
            return await FindSucceededAsync(bookingId) ?? throw new InvalidOperationException(
                $"Duplicate payment rejected for booking {bookingId}, but no succeeded payment was found.");
        }

        // Resultatet sendes som event via RabbitMQ. Booking Service lytter og
        // bekræfter bookingen, eller annullerer den hvis betalingen fejlede.
        if (success)
            await _eventPublisher.PublishPaymentSucceededAsync(new PaymentSucceededEvent
            {
                BookingId = bookingId,
                PaymentId = payment.Id,
                Amount = amount
            });
        else
            await _eventPublisher.PublishPaymentFailedAsync(new PaymentFailedEvent
            {
                BookingId = bookingId,
                PaymentId = payment.Id,
                Reason = payment.FailureReason ?? "Payment failed."
            });

        return payment;
    }

    public Task<Payment?> GetByBookingIdAsync(int bookingId)
    {
        return _db.Payments.AsNoTracking()
            .Where(p => p.BookingId == bookingId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private Task<Payment?> FindSucceededAsync(int bookingId) =>
        _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.BookingId == bookingId && p.Status == PaymentStatus.Succeeded);

    // Simuleret — lykkes altid for nu.
    private static bool SimulateGatewayCall() => true;
}
