using CarRental.Shared.Events;

namespace CarRental.PaymentService.Services;

// Payment Service fortæller resten af systemet om resultatet af en betaling
// via events. Den kender ikke Booking Service og venter ikke på den.
public interface IPaymentEventPublisher
{
    Task PublishPaymentSucceededAsync(PaymentSucceededEvent paymentSucceeded);

    // Krav: fejlet betaling annullerer reservationen.
    Task PublishPaymentFailedAsync(PaymentFailedEvent paymentFailed);
}
