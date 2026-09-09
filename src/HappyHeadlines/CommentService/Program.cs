
using CommentService.Data;
using CommentService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly.CircuitBreaker;
using Polly;

namespace CommentService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddHttpClient<ProfanityClient>(client =>
            {
                client.BaseAddress = new Uri(
                    builder.Configuration["Services:Profanity"]
                    ?? throw new InvalidOperationException("Services:Profanity is not configured."));
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddResilienceHandler("profanity", pipeline =>
            {
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 4,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15)
                });
            });

            builder.Services.AddDbContext<CommentDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Comments")));

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CommentDbContext>();
                db.Database.Migrate();
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
