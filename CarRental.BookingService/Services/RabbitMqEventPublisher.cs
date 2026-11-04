using CarRental.BookingService.Messaging;
using CarRental.Shared.Events;
using CarRental.Shared.Messaging;

namespace CarRental.BookingService.Services;

// Publicerer BookingConfirmedEvent til RabbitMQ. Notification Service (og
// enhver anden service der binder en kø til "booking.confirmed") modtager det.
// Booking Service kender ikke længere Notification Service's adresse.
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(RabbitMqPublisher publisher, ILogger<RabbitMqEventPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishBookingConfirmedAsync(BookingConfirmedEvent bookingConfirmed)
    {
        try
        {
            await _publisher.PublishAsync(MessagingTopology.RoutingKeys.BookingConfirmed, bookingConfirmed);
        }
        catch (Exception ex)
        {
            // Krav: en fejl i notifikationen må aldrig ramme en bekræftet booking.
            _logger.LogError(ex, "Could not publish BookingConfirmedEvent for booking {BookingId}", bookingConfirmed.BookingId);
        }
    }
}
