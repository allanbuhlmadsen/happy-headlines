using DraftService.Data;
using Microsoft.EntityFrameworkCore;
using Observability;

namespace DraftService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddObservability("DraftService");

            builder.Services.AddDbContext<DraftDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Drafts")));

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseObservability();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
                db.Database.Migrate();
            }

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.MapControllers();

            app.Run();
        }
    }
}