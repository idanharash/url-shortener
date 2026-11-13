using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace UrlShortener.Application.Health;

public static class HealthEndpoints
{
    public static IServiceCollection AddDependencyHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres")
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq");
        return services;
    }

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness: the process is up and serving requests. Runs no dependency checks, so a database
        // outage doesn't make an orchestrator restart healthy instances.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        // Readiness: dependency status. 200 for Healthy/Degraded, 503 for Unhealthy.
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteJson });
    }

    // Reports status and a short description only; exception details stay in the logs.
    private static Task WriteJson(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new { status = entry.Value.Status.ToString(), description = entry.Value.Description })
        });
}
