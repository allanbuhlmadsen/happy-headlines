using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Observability;
using RabbitMQ.Client;
using SubscriberService.Models;

namespace SubscriberService.Messaging;

public class SubscriberPublisher : IAsyncDisposable
{
    public const string ExchangeName = "subscribers";

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private SubscriberPublisher(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<SubscriberPublisher> CreateAsync(string hostName)
    {
        var factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = "guest",
            Password = "guest"
        };

        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);

        return new SubscriberPublisher(connection, channel);
    }

    public async Task PublishAsync(Subscriber subscriber)
    {
        using var activity = MessageTracing.ActivitySource.StartActivity(
            $"publish {ExchangeName}", ActivityKind.Producer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", ExchangeName);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(subscriber));

        var headers = new Dictionary<string, object?>();
        MessageTracing.Inject(activity ?? Activity.Current, headers);

        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = headers
        };

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}