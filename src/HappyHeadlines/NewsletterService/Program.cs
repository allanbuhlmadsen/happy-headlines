using FeatureHubSDK;
using Microsoft.Extensions.Http.Resilience;
using NewsletterService.Messaging;
using NewsletterService.Services;
using Observability;
using Polly;

namespace NewsletterService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddObservability("NewsletterService");

            var featureHub = new EdgeFeatureHubConfig(
                builder.Configuration["FeatureHub:Url"]
                    ?? throw new InvalidOperationException("FeatureHub:Url is not configured."),
                builder.Configuration["FeatureHub:ApiKey"]
                    ?? throw new InvalidOperationException("FeatureHub:ApiKey is not configured."));

            var features = await featureHub.NewContext().Build();
            builder.Services.AddSingleton(features);

            var articlesUrl = builder.Configuration["Services:Articles"]
                              ?? "http://loadbalancer:8080";

            builder.Services.AddHttpClient("articles", client =>
                client.BaseAddress = new Uri(articlesUrl));

            builder.Services.AddHttpClient<SubscriberClient>(client =>
            {
                client.BaseAddress = new Uri(
                    builder.Configuration["Services:Subscribers"]
                    ?? throw new InvalidOperationException("Services:Subscribers is not configured."));
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddResilienceHandler("subscribers", pipeline =>
            {
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 4,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15)
                });
            });

            builder.Services.AddHostedService<NewArticleListener>();
            builder.Services.AddHostedService<NewSubscriberListener>();

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