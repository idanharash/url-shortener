using Prometheus;
using UrlShortener.Application.Health;
using UrlShortener.BL;
using UrlShortener.Model.Service;

namespace UrlShortener.Application.Extensions;

public static class StartupExtensions
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<IUrlService, UrlService>();
        services.AddScoped<ICodeGeneratorService, CodeGeneratorService>();
        services.AddScoped<IStatsService, StatsService>();
    }

    public static void MapMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthEndpoints();
        app.MapMetrics();
    }
}
