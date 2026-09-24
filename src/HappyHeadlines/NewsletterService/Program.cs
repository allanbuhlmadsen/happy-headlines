using NewsletterService.Messaging;
using Observability;

namespace NewsletterService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddObservability("NewsletterService");

            var articlesUrl = builder.Configuration["Services:Articles"]
                              ?? "http://loadbalancer:8080";

            builder.Services.AddHttpClient("articles", client =>
                client.BaseAddress = new Uri(articlesUrl));

            builder.Services.AddHostedService<NewArticleListener>();

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseObservability();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapControllers();

            app.Run();
        }
    }
}