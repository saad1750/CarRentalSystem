namespace CarRental.BookingService.Messaging;

// Bindes fra "RabbitMQ"-sektionen i appsettings.json, som docker-compose
// overskriver med miljøvariabler (RabbitMQ__Host, RabbitMQ__Username, ...).
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
