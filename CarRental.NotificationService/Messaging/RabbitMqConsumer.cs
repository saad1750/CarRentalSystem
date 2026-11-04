using CarRental.Shared.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarRental.NotificationService.Messaging;

// Basisklasse for en baggrundstjeneste der lytter på én kø.
// Underklassen angiver kønavn, routing keys og hvad der skal ske med hver besked.
//
// - Køen er durable, så beskeder venter selvom servicen er nede.
// - En besked ack'es først når den er behandlet. Crasher servicen undervejs,
//   leveres beskeden igen.
// - Kaster handleren en fejl, nack'es beskeden og havner i dead-letter-køen
//   i stedet for at blive leveret igen og igen.
public abstract class RabbitMqConsumer : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly RabbitMqOptions _options;
    protected readonly ILogger Logger;

    private IConnection? _connection;
    private IChannel? _channel;

    protected RabbitMqConsumer(IOptions<RabbitMqOptions> options, ILogger logger)
    {
        _options = options.Value;
        Logger = logger;
    }

    protected abstract string QueueName { get; }
    protected abstract IReadOnlyCollection<string> RoutingKeys { get; }

    // Kaldes for hver besked. Kast en exception hvis beskeden ikke kan behandles.
    protected abstract Task HandleMessageAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // RabbitMQ er måske ikke klar endnu når containeren starter, så prøv igen.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                Logger.LogInformation("Listening on queue {Queue} for {RoutingKeys}", QueueName, string.Join(", ", RoutingKeys));
                return; // Forbindelsen genopretter sig selv herefter (automatic recovery).
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Logger.LogWarning(ex, "Could not connect to RabbitMQ at {Host}:{Port}, retrying in {Delay}s",
                    _options.Host, _options.Port, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            ClientProvidedName = $"{GetType().Assembly.GetName().Name} consumer ({QueueName})"
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Exchange for events + dead-letter-opsætning.
        await _channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.ExchangeDeclareAsync(MessagingTopology.DeadLetterExchange, ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(MessagingTopology.DeadLetterQueue,
            durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(MessagingTopology.DeadLetterQueue, MessagingTopology.DeadLetterExchange,
            routingKey: string.Empty, cancellationToken: cancellationToken);

        // Servicens egen kø, bundet til de events den vil have.
        await _channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = MessagingTopology.DeadLetterExchange },
            cancellationToken: cancellationToken);

        foreach (var routingKey in RoutingKeys)
            await _channel.QueueBindAsync(QueueName, MessagingTopology.EventsExchange, routingKey,
                cancellationToken: cancellationToken);

        // Højst 10 ubehandlede beskeder ad gangen til denne consumer.
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageAsync;

        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, cancellationToken);
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = ((AsyncEventingBasicConsumer)sender).Channel;

        try
        {
            await HandleMessageAsync(args.RoutingKey, args.Body, args.CancellationToken);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, args.CancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle message {MessageId} ({RoutingKey}) from {Queue}; sending to dead-letter queue",
                args.BasicProperties.MessageId, args.RoutingKey, QueueName);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, args.CancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
