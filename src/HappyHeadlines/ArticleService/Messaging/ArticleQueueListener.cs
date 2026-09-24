using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ArticleService.Data;
using ArticleService.Models;
using Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ArticleService.Messaging;

public class ArticleQueueListener : BackgroundService
{
    public const string ExchangeName = "articles";
    public const string QueueName = "article-queue-storage";

    private readonly ContinentDbContextFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<ArticleQueueListener> _log;

    private IConnection? _connection;
    private IChannel? _channel;

    public ArticleQueueListener(
        ContinentDbContextFactory factory,
        IConfiguration config,
        ILogger<ArticleQueueListener> log)
    {
        _factory = factory;
        _config = config;
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

        _log.LogInformation("Listening for articles on {QueueName}", QueueName);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        // Read the trace id the sender wrote into the message, and continue
        // that trace instead of starting a new one.
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
            var message = JsonSerializer.Deserialize<Article>(json);

            if (message is null)
            {
                _log.LogWarning("Discarded a message that could not be read");
                await _channel!.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            activity?.SetTag("article.continent", message.Continent);

            await using var db = _factory.Create(message.Continent);

            message.Id = 0;
            db.Articles.Add(message);
            await db.SaveChangesAsync();

            _log.LogInformation("Stored the article {Title} in {Continent}",
                message.Title, message.Continent);

            await _channel!.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to store an article from the queue");
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