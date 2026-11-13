using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;
using StackExchange.Redis;
using UrlShortener.Infrastructure.Messaging;

namespace UrlShortener.IntegrationTests;

[Collection(IntegrationTestsDefinition.Name)]
public sealed class FailureHandlingTests(UrlShortenerFixture fixture)
{
    [Fact]
    public async Task WhenRedisIsDown_RedirectsFallBackToTheDatabase_AndHealthIsDegraded()
    {
        // A second app instance whose Redis points at a closed port; PostgreSQL and RabbitMQ are real.
        await using var factory = new UrlShortenerFactory(services =>
        {
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = { "localhost:1" },
                AbortOnConnectFail = false,
                ConnectTimeout = 500,
                BacklogPolicy = BacklogPolicy.FailFast
            }));
        });
        using var client = UrlShortenerFixture.WithApiKey(
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));

        var created = await client.ShortenAsync("https://example.com/no-redis");
        using var redirect = await client.GetAsync($"/{created.Code}");
        var health = await client.GetFromJsonAsync<HealthReport>("/health");

        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal("https://example.com/no-redis", redirect.Headers.Location?.OriginalString);
        Assert.NotNull(health);
        Assert.Equal("Degraded", health.Status);
        Assert.Equal("Degraded", health.Checks["redis"].Status);
    }

    [Fact]
    public async Task MalformedClickMessage_IsMovedToTheDeadLetterQueue()
    {
        var deadLetterQueue = ClickQueueTopology.DeadLetterQueue(UrlShortenerFixture.QueueName);
        using var connection = fixture.ConnectToRabbitMq();
        using var channel = connection.CreateModel();
        var before = channel.MessageCount(deadLetterQueue);

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: UrlShortenerFixture.QueueName,
            basicProperties: channel.CreateBasicProperties(),
            body: "this is not a click message"u8.ToArray());

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (channel.MessageCount(deadLetterQueue) <= before && DateTime.UtcNow < deadline)
            await Task.Delay(100);

        Assert.Equal(before + 1, channel.MessageCount(deadLetterQueue));
    }
}
