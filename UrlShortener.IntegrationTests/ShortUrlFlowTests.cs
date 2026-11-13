using System.Net;
using System.Net.Http.Json;

namespace UrlShortener.IntegrationTests;

[Collection(IntegrationTestsDefinition.Name)]
public sealed class ShortUrlFlowTests(UrlShortenerFixture fixture)
{
    private static readonly string[] Dependencies = ["postgres", "rabbitmq", "redis"];

    [Fact]
    public async Task ShortenRedirectAndStats_WorkEndToEnd()
    {
        using var client = fixture.CreateClient();

        var created = await client.ShortenAsync("https://example.com/flow");
        Assert.Equal($"http://localhost/{created.Code}", created.ShortUrl);

        using var redirect = await client.GetAsync($"/{created.Code}");
        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal("https://example.com/flow", redirect.Headers.Location?.OriginalString);

        var stats = await client.WaitForClickCountAsync(created.Code, expected: 1);
        Assert.Equal(1, stats.ClickCount);
        Assert.Equal(DateTimeKind.Utc, stats.CreatedAt.Kind); // serialized with "Z"
        Assert.InRange(stats.CreatedAt, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task ConcurrentRedirects_AreCountedExactly()
    {
        const int clicks = 500;
        using var client = fixture.CreateClient();
        var code = (await client.ShortenAsync("https://example.com/concurrency")).Code;

        var responses = await Task.WhenAll(Enumerable.Range(0, clicks).Select(_ => client.GetAsync($"/{code}")));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Found, response.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        // Every click goes through the buffer, RabbitMQ and the consumer's atomic UPDATE: none lost, none doubled.
        var stats = await client.WaitForClickCountAsync(code, expected: clicks);
        Assert.Equal(clicks, stats.ClickCount);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url")]
    [InlineData("http://localhost/abc12")] // points at this service
    public async Task Shorten_RejectsInvalidOrUnsafeUrls(string url)
    {
        using var client = fixture.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/shorten", new { url });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/zzzzz")]           // well-formed but unknown code
    [InlineData("/favicon.ico")]     // malformed code, rejected without I/O
    [InlineData("/api/stats/zzzzz")]
    public async Task UnknownCodes_Return404(string path)
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReportsAllDependenciesHealthy()
    {
        using var client = fixture.CreateClient();

        var report = await client.GetFromJsonAsync<HealthReport>("/health");

        Assert.NotNull(report);
        Assert.Equal("Healthy", report.Status);
        Assert.Equal(Dependencies, report.Checks.Keys.Order());
    }
}
