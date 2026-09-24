using Observability;
namespace Webapp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddObservability("Webapp");

            // Add services to the container.
            builder.Services.AddRazorPages();

            var publisherUrl = builder.Configuration["Services:Publisher"]
                               ?? "http://publisherservice:8080";

            builder.Services.AddHttpClient("publisher", client =>
                client.BaseAddress = new Uri(publisherUrl));

            var app = builder.Build();

            app.UseObservability();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();

            app.Run();
        }
    }
}
