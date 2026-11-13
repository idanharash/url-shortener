using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Prometheus;
using UrlShortener.Application.Health;
using UrlShortener.BL;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.Application.Extensions;

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
    }

    public static void AddResiliencePolicies(this IServiceCollection services)
    {
        // Only transient errors (connection drops, timeouts) are retried or count towards the breaker;
        // retrying a constraint violation or a SQL bug just delays the same failure.
        // Retry is the outer strategy, so an open circuit fails fast instead of being retried.
        services.AddResiliencePipeline(UrlRepository.PipelineName, builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(TransientDbErrors.IsTransient),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(TransientDbErrors.IsTransient),
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });
        });
    }

    public static void MapMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthEndpoints();
        app.MapMetrics();
    }
}
