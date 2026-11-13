using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using UrlShortener.Application.Controllers;
using UrlShortener.Application.Security;
using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Migrations.Migrations;
using UrlShortener.Worker;

namespace UrlShortener.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestsDefinition : ICollectionFixture<UrlShortenerFixture>
{
    public const string Name = "Integration";
}

/// <summary>
/// Starts PostgreSQL, Redis and RabbitMQ once for all integration tests, applies the real migrations and
/// runs the real API and the real Worker in memory, so clicks travel API → RabbitMQ → Worker → PostgreSQL.
/// </summary>
[SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit calls IAsyncLifetime.DisposeAsync, which disposes the factory, the Worker and the containers.")]
public sealed class UrlShortenerFixture : IAsyncLifetime
{
    public const string QueueName = "click-events";
    public const string ApiKey = "integration-test-key-0123456789abcdef";
    private const string RabbitMqUser = "test";
    private const string RabbitMqPassword = "test";
    private const ushort RabbitMqPort = 5672;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("url_shortener")
        .WithUsername("postgres")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management")
        .WithUsername(RabbitMqUser)
        .WithPassword(RabbitMqPassword)
        .Build();

    private UrlShortenerFactory? _factory;
    private IHost? _worker;

    public UrlShortenerFactory Factory => _factory ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbitMq.StartAsync());
        ApplyMigrations();

        // Environment variables are read by WebApplication.CreateBuilder before Program registers services,
        // so the app validates and uses exactly these settings.
        var postgres = new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());
        SetSetting("Postgres__Host", postgres.Host!);
        SetSetting("Postgres__Port", postgres.Port.ToString(CultureInfo.InvariantCulture));
        SetSetting("Postgres__Database", postgres.Database!);
        SetSetting("Postgres__Username", postgres.Username!);
        SetSetting("Postgres__Password", postgres.Password!);
        SetSetting("Redis__ConnectionString", _redis.GetConnectionString());
        SetSetting("RabbitMQ__Host", _rabbitMq.Hostname);
        SetSetting("RabbitMQ__Port", _rabbitMq.GetMappedPublicPort(RabbitMqPort).ToString(CultureInfo.InvariantCulture));
        SetSetting("RabbitMQ__Username", RabbitMqUser);
        SetSetting("RabbitMQ__Password", RabbitMqPassword);
        SetSetting("RabbitMQ__QueueName", QueueName);
        SetSetting("App__PublicBaseUrl", "http://localhost");
        SetSetting("Auth__ApiKeys__0", ApiKey);
        SetSetting("RateLimiting__PermitLimit", "100000"); // the tests create many links quickly
        SetSetting("Worker__MetricsPort", FreeTcpPort().ToString(CultureInfo.InvariantCulture));

        _worker = WorkerHost.CreateBuilder([]).Build();
        await _worker.StartAsync();

        _factory = new UrlShortenerFactory();
        await WaitUntilReadyAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();

        if (_worker is not null)
        {
            await _worker.StopAsync();
            _worker.Dispose();
        }

        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }

    /// <summary>A client that sends the API key, as a real API consumer would.</summary>
    public HttpClient CreateClient() => WithApiKey(CreateAnonymousClient());

    /// <summary>A client without the API key, e.g. a browser following a short link.</summary>
    public HttpClient CreateAnonymousClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static HttpClient WithApiKey(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, ApiKey);
        return client;
    }

    public IConnection ConnectToRabbitMq() => new ConnectionFactory
    {
        HostName = _rabbitMq.Hostname,
        Port = _rabbitMq.GetMappedPublicPort(RabbitMqPort),
        UserName = RabbitMqUser,
        Password = RabbitMqPassword
    }.CreateConnection();

    private void ApplyMigrations()
    {
        using var services = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(_postgres.GetConnectionString())
                .ScanIn(typeof(InitialCreate).Assembly).For.Migrations())
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }

    // Starts the API and waits until its publisher and the Worker's consumer are connected to RabbitMQ.
    private async Task WaitUntilReadyAsync()
    {
        using var client = CreateClient();
        var consumer = _worker!.Services.GetRequiredService<ClickQueueConsumer>();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var report = await client.GetFromJsonAsync<HealthReport>("/health");
            if (report?.Status == "Healthy" && consumer.IsConsuming)
                return;
            await Task.Delay(250);
        }

        throw new TimeoutException("The API and the Worker did not become ready within 60 seconds.");
    }

    // The Worker's /metrics endpoint needs a port; pick a free one so tests never clash with a running Worker.
    private static int FreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void SetSetting(string name, string value) => Environment.SetEnvironmentVariable(name, value);
}

/// <summary>The real application. Optional service overrides let a test simulate a failing dependency.</summary>
public sealed class UrlShortenerFactory(Action<IServiceCollection>? overrideServices = null)
    : WebApplicationFactory<UrlController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program uses the output folder as its content root (where SharedConfig/appsettings.json is copied);
        // use the same one so the host configuration doesn't conflict.
        builder.UseContentRoot(AppContext.BaseDirectory);

        if (overrideServices is not null)
            builder.ConfigureTestServices(overrideServices);
    }
}

public sealed record HealthReport(string Status, Dictionary<string, HealthEntry> Checks);

public sealed record HealthEntry(string Status, string? Description);
