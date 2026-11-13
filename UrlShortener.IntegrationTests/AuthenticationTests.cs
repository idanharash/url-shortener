using System.Net;
using System.Net.Http.Json;
using UrlShortener.Application.Security;

namespace UrlShortener.IntegrationTests;

[Collection(IntegrationTestsDefinition.Name)]
public sealed class AuthenticationTests(UrlShortenerFixture fixture)
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-the-configured-key-0123456789abcdef")]
    public async Task Shorten_WithoutAValidApiKey_Returns401(string? apiKey)
    {
        using var client = fixture.CreateAnonymousClient();
        if (apiKey is not null)
            client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, apiKey);

        using var response = await client.PostAsJsonAsync("/api/shorten", new { url = "https://example.com/no-key" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RedirectsAndStats_StayPublic()
    {
        using var authorized = fixture.CreateClient();
        var code = (await authorized.ShortenAsync("https://example.com/public")).Code;

        using var anonymous = fixture.CreateAnonymousClient();
        using var redirect = await anonymous.GetAsync($"/{code}");
        using var stats = await anonymous.GetAsync($"/api/stats/{code}");

        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
    }
}
