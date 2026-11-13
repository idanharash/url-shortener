using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StackExchange.Redis;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Infrastructure.Observability;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.Infrastructure.DependencyInjection;

/// <summary>
/// Registrations shared by the API and the Worker. Each host picks only what it needs:
/// the API uses PostgreSQL, Redis and the click publisher; the Worker uses PostgreSQL and the click consumer.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration config)
    {
        var postgres = services.AddValidatedSettings<PostgresSettings>(config, PostgresSettings.SectionName);
        services.AddSingleton(Fluently.Configure()
            .Database(PostgreSQLConfiguration.Standard.ConnectionString(postgres.ToConnectionString()))
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<UrlRepository>())
            .BuildSessionFactory());

        // Only transient errors (connection drops, timeouts) are retried or count towards the breaker;
        // retrying a constraint violation or a SQL bug just delays the same failure.
        // Retry is the outer strategy, so an open circuit fails fast instead of being retried.
        services.AddResiliencePipeline(UrlRepository.PipelineName, builder => builder
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
            }));

        services.AddScoped<IUrlRepository, UrlRepository>();
        return services;
    }

    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration config)
    {
        var redis = services.AddValidatedSettings<RedisSettings>(config, RedisSettings.SectionName);
        services.AddValidatedSettings<CacheSettings>(config, CacheSettings.SectionName);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redis.ConnectionString);
            options.AbortOnConnectFail = false; // start even if Redis is down; it reconnects in the background
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 1000;
            // While disconnected, fail commands immediately instead of queueing them until the timeout:
            // the cache falls back to the database, so waiting would only slow down redirects.
            options.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<ICacheService, CacheService>();
        return services;
    }

    /// <summary>Buffers clicks in memory and publishes them to RabbitMQ (used by the API).</summary>
    public static IServiceCollection AddClickPublisher(this IServiceCollection services, IConfiguration config)
    {
        services.AddValidatedSettings<RabbitMqSettings>(config, RabbitMqSettings.SectionName);

        // One instance serves both roles: request threads enqueue clicks, the hosted service publishes them.
        services.AddSingleton<ClickQueueProducer>();
        services.AddSingleton<IClickQueueProducer>(sp => sp.GetRequiredService<ClickQueueProducer>());
        services.AddHostedService(sp => sp.GetRequiredService<ClickQueueProducer>());
        return services;
    }

    /// <summary>Consumes clicks from RabbitMQ and stores them (used by the Worker). Requires an IClickHandlerService.</summary>
    public static IServiceCollection AddClickConsumer(this IServiceCollection services, IConfiguration config)
    {
        services.AddValidatedSettings<RabbitMqSettings>(config, RabbitMqSettings.SectionName);
        services.AddSingleton<ClickQueueConsumer>();
        services.AddHostedService(sp => sp.GetRequiredService<ClickQueueConsumer>());
        return services;
    }

    /// <summary>OpenTelemetry tracing to the configured OTLP endpoint, plus host-specific instrumentation.</summary>
    public static IServiceCollection AddTracing(this IServiceCollection services, IConfiguration config,
        Action<TracerProviderBuilder>? configure = null)
    {
        var settings = services.AddValidatedSettings<OpenTelemetrySettings>(config, OpenTelemetrySettings.SectionName);

        services.AddOpenTelemetry().WithTracing(builder =>
        {
            builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(settings.ServiceName))
                // Fine at this scale; at high traffic, switch to ParentBased(TraceIdRatioBased) sampling.
                .SetSampler(new AlwaysOnSampler())
                .AddSource(Telemetry.Cache.Name, Telemetry.Database.Name, Telemetry.Messaging.Name)
                .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(settings.OtlpEndpoint));

            configure?.Invoke(builder);

            // Printing every span costs about 4x throughput under load (see OpenTelemetrySettings.ConsoleExporter).
            if (settings.ConsoleExporter)
                builder.AddConsoleExporter();
        });
        return services;
    }
}
