using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UrlShortener.Application.Settings;
using UrlShortener.Infrastructure.Observability;

namespace UrlShortener.Application.Extensions;

public static class TelemetryStartupExtensions
{
    public static void AddTelemetry(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        var settings = services.AddValidatedSettings<OpenTelemetrySettings>(config, OpenTelemetrySettings.SectionName);

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(settings.ServiceName))
                    // Fine at this scale; at high traffic, switch to ParentBased(TraceIdRatioBased) sampling.
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health") &&
                                                    !context.Request.Path.StartsWithSegments("/metrics"))
                    .AddHttpClientInstrumentation()
                    .AddSource(Telemetry.Cache.Name, Telemetry.Database.Name, Telemetry.Messaging.Name)
                    .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(settings.OtlpEndpoint));

                // Printing every span is useful locally but too costly and noisy in production.
                if (environment.IsDevelopment())
                    builder.AddConsoleExporter();
            });
    }
}
