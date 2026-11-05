using FeatureHubSDK;
using Microsoft.EntityFrameworkCore;
using Observability;
using SubscriberService.Data;
using SubscriberService.Messaging;

namespace SubscriberService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddObservability("SubscriberService");

            var featureHub = new EdgeFeatureHubConfig(
                builder.Configuration["FeatureHub:Url"]
                    ?? throw new InvalidOperationException("FeatureHub:Url is not configured."),
                builder.Configuration["FeatureHub:ApiKey"]
                    ?? throw new InvalidOperationException("FeatureHub:ApiKey is not configured."));

            var features = await featureHub.NewContext().Build();
            builder.Services.AddSingleton(features);

            builder.Services.AddDbContext<SubscriberDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Subscribers")));

            var rabbitHost = builder.Configuration["Rabbit:Host"] ?? "rabbitmq";
            var publisher = await SubscriberPublisher.CreateAsync(rabbitHost);
            builder.Services.AddSingleton(publisher);

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseObservability();

            // The whole service is behind the release toggle. When the flag is
            // off, every endpoint answers 503 — no redeployment involved.
            app.Use(async (context, next) =>
            {
                var toggles = context.RequestServices.GetRequiredService<IClientContext>();

                if (!toggles["SUBSCRIBERS_ENABLED"].IsEnabled)
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsync("The subscriber feature is turned off.");
                    return;
                }

                await next();
            });

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SubscriberDbContext>();
                db.Database.Migrate();
            }

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}