using System.Text.Json;
using CarRental.Shared.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CarRental.PaymentService.Messaging;

// Publicerer events til topic-exchangen "carrental.events".
// Registreres som singleton: én forbindelse og én kanal genbruges af hele servicen.
public sealed class RabbitMqPublisher : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string routingKey, T message, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent, // overlever genstart af RabbitMQ
            MessageId = Guid.NewGuid().ToString(),
            Type = typeof(T).Name,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        // Kanalen deles, så publish-kald køres ét ad gangen.
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);

            // Publisher confirms er slået til, så kaldet venter til RabbitMQ har modtaget beskeden.
            await channel.BasicPublishAsync(
                MessagingTopology.EventsExchange, routingKey, mandatory: false,
                properties, body, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("Published {EventType} with routing key {RoutingKey}", typeof(T).Name, routingKey);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            ClientProvidedName = $"{typeof(RabbitMqPublisher).Assembly.GetName().Name} publisher"
        };

        _connection ??= await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        // Idempotent: opretter exchangen hvis den ikke findes.
        await _channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
