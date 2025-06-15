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

var builder = WebApplication.CreateBuilder(args);
var sharedConfigPath = Path.GetFullPath(Path.Combine("..", "SharedConfig", "appsettings.json"));
builder.Configuration
    .AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var connectionString = builder.Configuration["ConnectionStrings:Postgres"];
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("❌ Missing Postgres connection string");

if (string.IsNullOrWhiteSpace(redisConnectionString))
    throw new InvalidOperationException("❌ Missing Redis connection string");

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
        EndPoints = { redisConnectionString! },
        ConnectTimeout = 2000,
        AbortOnConnectFail = false
    }));

// NHibernate
var sessionFactory = NHibernateHelper.CreateSessionFactory(connectionString!);
builder.Services.AddSingleton(sessionFactory);

// RabbitMQ
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));
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

// Application Services
builder.Services.AddScoped<IUrlRepository, UrlRepository>();
builder.Services.AddScoped<IUrlService, UrlService>();
builder.Services.AddScoped<ICodeGeneratorService, CodeGeneratorService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IClickHandlerService, ClickHandlerService>();
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddHostedService<ClickQueueConsumer>();

// Observability & Controllers
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// Resilience
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

// Middleware
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
