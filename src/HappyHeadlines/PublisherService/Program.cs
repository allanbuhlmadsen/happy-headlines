using Observability;
using PublisherService.Messaging;

namespace PublisherService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddObservability("PublisherService");

            var rabbitHost = builder.Configuration["Rabbit:Host"] ?? "rabbitmq";
            var publisher = await ArticlePublisher.CreateAsync(rabbitHost);
            builder.Services.AddSingleton(publisher);

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseObservability();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapControllers();

            await app.RunAsync();
        }
    }
}