using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;
using UrlShortener.BL;
using Prometheus;
using UrlShortener.Model.Observability;

namespace UrlShortener.Infrastructure.Extensions;

public static class StartupExtensions
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<IUrlRepository, UrlRepository>();
        services.AddScoped<IUrlService, UrlService>();
        services.AddScoped<ICodeGeneratorService, CodeGeneratorService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IClickHandlerService, ClickHandlerService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IAppTracer, AppTracer>();
    }

    public static void AddResiliencePolicies(this IServiceCollection services)
    {
        services.AddResiliencePipeline("db-pipeline", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });
        });
    }

    public static void MapMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok("Healthy"));
        app.MapMetrics();
    }
}
