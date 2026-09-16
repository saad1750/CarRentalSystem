using System.Net.Http.Json;
using CarRental.Shared.Events;

namespace CarRental.BookingService.Services;

// MIDLERTIDIG: kalder Notification Service direkte over REST i stedet for en
// rigtig message broker. Skift senere til RabbitMQ/Kafka uden at ændre
// resten af koden, da alt kun kender til IEventPublisher.
public class HttpEventPublisher : IEventPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpEventPublisher> _logger;

    public HttpEventPublisher(IHttpClientFactory httpClientFactory, ILogger<HttpEventPublisher> logger)
    {
        _httpClient = httpClientFactory.CreateClient("NotificationService");
        _logger = logger;
    }

    public async Task PublishBookingConfirmedAsync(BookingConfirmedEvent bookingConfirmed)
    {
        try
        {
            // En fejlende Notification Service må aldrig påvirke en bekræftet booking.
            await _httpClient.PostAsJsonAsync("api/notification/booking-confirmed", bookingConfirmed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reach Notification Service for booking {BookingId}", bookingConfirmed.BookingId);
        }
    }
}
