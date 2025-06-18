using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UrlShortener.BL;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;
using Serilog;
using Serilog.Enrichers.Span;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using UrlShortener.Infrastructure;
using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Infrastructure.Caching;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | TraceId={TraceId} SpanId={SpanId}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

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

builder.Services.AddOpenTelemetry()
    .WithTracing(b =>
    {
        b.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(builder.Configuration["OpenTelemetry:Tracing:ServiceName"] ?? "UrlShortener"));
        b.AddAspNetCoreInstrumentation();
        b.AddHttpClientInstrumentation();
        b.AddSource("UrlShortener.Messaging");
        b.AddSource("UrlShortener.Database");
        b.AddJaegerExporter(opt =>
        {
            opt.AgentHost = builder.Configuration["OpenTelemetry:Tracing:Jaeger:AgentHost"] ?? "localhost"; ;
            opt.AgentPort = int.TryParse(builder.Configuration["OpenTelemetry:Tracing:Jaeger:AgentPort"], out var port) ? port : 6831;
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


app.UseHttpMetrics();
app.UseRouting();
app.UseAuthorization();

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

app.MapMetrics();
app.MapControllers();


app.Run();
