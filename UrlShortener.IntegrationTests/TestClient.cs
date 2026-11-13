using System.Net;
using System.Net.Http.Json;
using UrlShortener.Model;

namespace UrlShortener.IntegrationTests;

internal static class TestClient
{
    public static async Task<ShortenUrlResponse> ShortenAsync(this HttpClient client, string url)
    {
        using var response = await client.PostAsJsonAsync("/api/shorten", new { url });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShortenUrlResponse>())!;
    }

    /// <summary>
    /// Clicks are counted asynchronously, so poll until the expected count is reached, then wait a moment
    /// longer to make sure no extra (duplicate) clicks arrive.
    /// </summary>
    public static async Task<GetStatsByCodeResponse> WaitForClickCountAsync(this HttpClient client, string code, long expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        GetStatsByCodeResponse stats;
        do
        {
            stats = (await client.GetFromJsonAsync<GetStatsByCodeResponse>($"/api/stats/{code}"))!;
            if (stats.ClickCount >= expected)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return (await client.GetFromJsonAsync<GetStatsByCodeResponse>($"/api/stats/{code}"))!;
            }

            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);

        return stats;
    }
}
