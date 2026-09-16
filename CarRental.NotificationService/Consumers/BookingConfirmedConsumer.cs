namespace CarRental.NotificationService.Consumers;

// PLACEHOLDER: når RabbitMQ/Kafka er sat op, laves en BackgroundService her
// der lytter på "booking-confirmed"-køen og kalder
// INotificationService.NotifyBookingConfirmedAsync for hver besked.
// Indtil da bruges NotificationController i stedet.
//
// public class BookingConfirmedConsumer : BackgroundService
// {
//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         // abonnér på køen, deserialisér BookingConfirmedEvent, kald
//         // INotificationService.NotifyBookingConfirmedAsync(event)
//     }
// }
