using ArticleService.Messaging;
using Observability;
using ArticleService.Data;
using Microsoft.EntityFrameworkCore;

namespace ArticleService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddObservability("ArticleService");

            builder.Services.AddDbContextFactory<ArticleDbContext>();

            builder.Services.AddSingleton<ContinentDbContextFactory>();

            builder.Services.AddHostedService<ArticleQueueListener>();

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseObservability();

            using (var scope = app.Services.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<ContinentDbContextFactory>();

                foreach (var continent in factory.Continents)
                {
                    using var db = factory.Create(continent);
                    db.Database.Migrate();
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
