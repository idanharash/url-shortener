using Prometheus;
using UrlShortener.BL;
using UrlShortener.Infrastructure.DependencyInjection;
using UrlShortener.Model.Service;

namespace UrlShortener.Worker;

/// <summary>
/// Composition root of the Worker, shared by Program and the integration tests so both run the same setup.
/// </summary>
public static class WorkerHost
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            // Same SharedConfig/appsettings.json as the API (linked into the output folder), with user-secrets
            // (Development) and environment variables on top.
            ContentRootPath = AppContext.BaseDirectory
        });

        var settings = builder.Services.AddValidatedSettings<WorkerSettings>(builder.Configuration, WorkerSettings.SectionName);
        builder.Services.AddPostgres(builder.Configuration);
        builder.Services.AddClickConsumer(builder.Configuration);
        builder.Services.AddScoped<IClickHandlerService, ClickHandlerService>();
        builder.Services.AddTracing(builder.Configuration);
        builder.Services.AddMetricServer(options => options.Port = (ushort)settings.MetricsPort);

        return builder;
    }
}
