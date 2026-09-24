using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Observability;
using PublisherService.Models;
using RabbitMQ.Client;

namespace PublisherService.Messaging;

public class ArticlePublisher : IAsyncDisposable
{
    public const string ExchangeName = "articles";

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private ArticlePublisher(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<ArticlePublisher> CreateAsync(string hostName)
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

        return new ArticlePublisher(connection, channel);
    }

    public async Task PublishAsync(Article article)
    {
        // A span for the act of sending. The receivers' spans will point back
        // to this one, so the halves show up as one trace in Zipkin.
        using var activity = MessageTracing.ActivitySource.StartActivity(
            $"publish {ExchangeName}", ActivityKind.Producer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", ExchangeName);
        activity?.SetTag("article.continent", article.Continent);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(article));

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