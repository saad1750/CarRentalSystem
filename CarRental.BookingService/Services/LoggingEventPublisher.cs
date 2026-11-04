using CarRental.Shared.Events;

namespace CarRental.BookingService.Services;

// Simpel stand-in der kun logger.
// NB: bruges ikke — RabbitMqEventPublisher er registreret i Program.cs.
// Kan bruges i tests eller hvis man vil køre uden RabbitMQ.
public class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishBookingConfirmedAsync(BookingConfirmedEvent bookingConfirmed)
    {
        _logger.LogInformation(
            "BookingConfirmedEvent published for booking {BookingId}",
            bookingConfirmed.BookingId);

        return Task.CompletedTask;
    }
}
