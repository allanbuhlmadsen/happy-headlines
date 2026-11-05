using FeatureHubSDK;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NewsletterService.Models;
using Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NewsletterService.Messaging;

public class NewSubscriberListener : BackgroundService
{
    public const string ExchangeName = "subscribers";
    public const string QueueName = "subscriber-queue-newsletter";

    private readonly IConfiguration _config;
    private readonly IClientContext _features;
    private readonly ILogger<NewSubscriberListener> _log;

    private IConnection? _connection;
    private IChannel? _channel;

    public NewSubscriberListener(
        IConfiguration config,
        IClientContext features,
        ILogger<NewSubscriberListener> log)
    {
        _config = config;
        _features = features;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = _config["Rabbit:Host"] ?? "rabbitmq",
            UserName = "guest",
            Password = "guest"
        };

        _connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _log.LogInformation("Listening for new subscribers on {QueueName}", QueueName);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        // Continue the trace the publisher started, instead of beginning a new one.
        var parentContext = MessageTracing.Extract(args.BasicProperties.Headers);

        using var activity = MessageTracing.ActivitySource.StartActivity(
            $"receive {QueueName}",
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.source.name", QueueName);

        try
        {
            var json = Encoding.UTF8.GetString(args.Body.ToArray());
            var subscriber = JsonSerializer.Deserialize<Subscriber>(json);

            if (subscriber is null)
            {
                _log.LogWarning("Discarded a message that could not be read");
                await _channel!.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            activity?.SetTag("subscriber.id", subscriber.Id);

            if (!_features["SUBSCRIBERS_ENABLED"].IsEnabled)
            {
                _log.LogInformation(
                    "The subscriber feature is turned off, so no welcome mail was sent to {Email}",
                    subscriber.Email);

                await _channel!.BasicAckAsync(args.DeliveryTag, false);
                return;
            }

            // The Email System is outside the system boundary and is not built,
            // so the welcome mail is a log entry.
            _log.LogInformation(
                "Sent a welcome mail to {Email} for subscriber {SubscriberId}",
                subscriber.Email, subscriber.Id);

            await _channel!.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to handle a new subscriber from the queue");
            await _channel!.BasicNackAsync(args.DeliveryTag, false, true);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}