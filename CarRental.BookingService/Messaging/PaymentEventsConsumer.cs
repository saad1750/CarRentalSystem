using System.Text.Json;
using CarRental.BookingService.Services;
using CarRental.Shared.Events;
using CarRental.Shared.Messaging;
using Microsoft.Extensions.Options;

namespace CarRental.BookingService.Messaging;

// Lytter på betalings-events fra Payment Service:
//   payment.succeeded -> bookingen bekræftes (og BookingConfirmedEvent sendes videre)
//   payment.failed    -> bookingen sættes til Failed og bilen frigives (saga-kompensation)
public class PaymentEventsConsumer : RabbitMqConsumer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;

    public PaymentEventsConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<PaymentEventsConsumer> logger)
        : base(options, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override string QueueName => "booking-service.payment-events";

    protected override IReadOnlyCollection<string> RoutingKeys =>
    [
        MessagingTopology.RoutingKeys.PaymentSucceeded,
        MessagingTopology.RoutingKeys.PaymentFailed
    ];

    protected override async Task HandleMessageAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        // IBookingService er scoped, så der laves et scope pr. besked (ligesom pr. HTTP-request).
        using var scope = _scopeFactory.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        switch (routingKey)
        {
            case MessagingTopology.RoutingKeys.PaymentSucceeded:
            {
                var evt = JsonSerializer.Deserialize<PaymentSucceededEvent>(body.Span, JsonOptions)
                          ?? throw new InvalidOperationException("Empty PaymentSucceededEvent");

                var result = await bookingService.ConfirmBookingAsync(evt.BookingId);
                if (result.Success)
                    Logger.LogInformation("Booking {BookingId} confirmed after payment {PaymentId}", evt.BookingId, evt.PaymentId);
                else
                    Logger.LogWarning("Could not confirm booking {BookingId}: {Error}", evt.BookingId, result.Error);
                break;
            }

            case MessagingTopology.RoutingKeys.PaymentFailed:
            {
                var evt = JsonSerializer.Deserialize<PaymentFailedEvent>(body.Span, JsonOptions)
                          ?? throw new InvalidOperationException("Empty PaymentFailedEvent");

                await bookingService.HandlePaymentFailedAsync(evt.BookingId);
                Logger.LogInformation("Booking {BookingId} failed after payment {PaymentId}: {Reason}", evt.BookingId, evt.PaymentId, evt.Reason);
                break;
            }

            default:
                Logger.LogWarning("Ignoring message with unexpected routing key {RoutingKey}", routingKey);
                break;
        }
    }
}
