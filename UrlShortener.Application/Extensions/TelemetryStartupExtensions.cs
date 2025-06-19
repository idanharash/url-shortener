using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class TelemetryStartupExtensions
{
    public static void AddTelemetry(this IServiceCollection services, IConfiguration config)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(config["OpenTelemetry:Tracing:ServiceName"] ?? "UrlShortener"))
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("UrlShortener.Application")
                    .AddSource("UrlShortener.Cache")
                    .AddSource("UrlShortener.Database")
                    .AddSource("UrlShortener.Messaging")
                    .AddConsoleExporter(opt =>
                    {
                        opt.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
                    });
            });
    }
}
