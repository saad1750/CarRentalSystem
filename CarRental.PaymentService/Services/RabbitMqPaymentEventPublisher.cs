using CarRental.PaymentService.Messaging;
using CarRental.Shared.Events;
using CarRental.Shared.Messaging;

namespace CarRental.PaymentService.Services;

public class RabbitMqPaymentEventPublisher : IPaymentEventPublisher
{
    private readonly RabbitMqPublisher _publisher;

    public RabbitMqPaymentEventPublisher(RabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task PublishPaymentSucceededAsync(PaymentSucceededEvent paymentSucceeded) =>
        _publisher.PublishAsync(MessagingTopology.RoutingKeys.PaymentSucceeded, paymentSucceeded);

    public Task PublishPaymentFailedAsync(PaymentFailedEvent paymentFailed) =>
        _publisher.PublishAsync(MessagingTopology.RoutingKeys.PaymentFailed, paymentFailed);
}
