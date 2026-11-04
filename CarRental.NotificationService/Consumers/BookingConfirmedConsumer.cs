using System.Text.Json;
using CarRental.NotificationService.Messaging;
using CarRental.NotificationService.Services;
using CarRental.Shared.Events;
using CarRental.Shared.Messaging;
using Microsoft.Extensions.Options;

namespace CarRental.NotificationService.Consumers;

// Lytter på "booking.confirmed" fra Booking Service og sender bekræftelse
// via email/SMS. Er Notification Service nede, venter beskederne i køen og
// behandles når servicen kommer op igen — bookingen påvirkes aldrig.
public class BookingConfirmedConsumer : RabbitMqConsumer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly INotificationService _notificationService;

    public BookingConfirmedConsumer(
        INotificationService notificationService,
        IOptions<RabbitMqOptions> options,
        ILogger<BookingConfirmedConsumer> logger)
        : base(options, logger)
    {
        _notificationService = notificationService;
    }

    protected override string QueueName => "notification-service.booking-confirmed";

    protected override IReadOnlyCollection<string> RoutingKeys => [MessagingTopology.RoutingKeys.BookingConfirmed];

    protected override async Task HandleMessageAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var bookingConfirmed = JsonSerializer.Deserialize<BookingConfirmedEvent>(body.Span, JsonOptions)
                               ?? throw new InvalidOperationException("Empty BookingConfirmedEvent");

        Logger.LogInformation("Received BookingConfirmedEvent for booking {BookingId}", bookingConfirmed.BookingId);

        // Kaster aldrig — retry håndteres inde i NotificationService.
        await _notificationService.NotifyBookingConfirmedAsync(bookingConfirmed);
    }
}
