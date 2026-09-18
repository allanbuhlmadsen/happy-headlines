using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace Observability;

public static class ObservabilitySetup
{
    // Logging with Serilog to Seq, tracing with OpenTelemetry to Zipkin.
    // Every service calls this once, with its own name.
    public static WebApplicationBuilder AddObservability(
        this WebApplicationBuilder builder, string serviceName)
    {
        var seqUrl = builder.Configuration["Observability:SeqUrl"]
                     ?? "http://seq:5341";
        var zipkinUrl = builder.Configuration["Observability:ZipkinUrl"]
                        ?? "http://zipkin:9411/api/v2/spans";

        builder.Host.UseSerilog((context, logger) => logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new ActivityEnricher())
            .Enrich.WithProperty("ServiceName", serviceName)
            .WriteTo.Console()
            .WriteTo.Seq(seqUrl));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql")
                .AddZipkinExporter(options =>
                    options.Endpoint = new Uri(zipkinUrl)));

        return builder;
    }

    // One log entry per HTTP request, with method, path, status code and duration.
    public static WebApplication UseObservability(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        return app;
    }
}