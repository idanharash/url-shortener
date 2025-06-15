using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StackExchange.Redis;
using UrlShortener.Application;
using UrlShortener.BL;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

ThreadPool.SetMinThreads(100, 100);

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
var redisConnectionString = Environment.GetEnvironmentVariable("Redis__ConnectionString");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Missing Postgres connection string (ConnectionStrings__Postgres)");

if (string.IsNullOrWhiteSpace(redisConnectionString))
    throw new InvalidOperationException("Missing Redis connection string (Redis__ConnectionString)");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
        EndPoints = { redisConnectionString! },
        ConnectTimeout = 2000,
        AbortOnConnectFail = false
    }));

var sessionFactory = NHibernateHelper.CreateSessionFactory(connectionString!);
builder.Services.AddSingleton(sessionFactory);

builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));

builder.Services.AddHealthChecks();

builder.Services.AddSingleton<IClickQueueProducer>(sp =>
{
    try
    {
        var config = sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
        return new ClickQueueProducer(config);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RabbitMQ Producer Init Error]: {ex.Message}");
        throw;
    }
});

builder.Services.AddScoped<IUrlRepository, UrlRepository>();
builder.Services.AddScoped<IUrlService, UrlService>();
builder.Services.AddScoped<ICodeGeneratorService, CodeGeneratorService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IClickHandlerService, ClickHandlerService>();
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddHostedService<ClickQueueConsumer>();

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddResiliencePipeline("db-pipeline", pipelineBuilder =>
{
    pipelineBuilder
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

var app = builder.Build();

app.UseMiddleware<MetricsMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapGet("/metrics", () =>
{
    var (requests, errors) = MetricsMiddleware.GetMetrics();
    return Results.Ok(new
    {
        requests,
        errors,
        CacheService.CacheHits,
        CacheService.CacheMisses
    });
});

app.MapControllers();

app.Run();
