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

    /// <summary>
    /// With a management port configured, /health and /metrics answer 404 on every other port.
    /// Checks the port the request arrived on, not the Host header, which a client controls.
    /// </summary>
    public static IApplicationBuilder UseManagementPort(this IApplicationBuilder app, int? managementPort)
    {
        if (managementPort is not { } port)
            return app;

        return app.Use(async (context, next) =>
        {
            if (context.Connection.LocalPort != port && IsMonitoringPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(context);
        });
    }

    private static bool IsMonitoringPath(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/metrics");
}
