using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Model.Repository;

namespace UrlShortener.IntegrationTests;

[Collection(IntegrationTestsDefinition.Name)]
public sealed class PersistenceTests(UrlShortenerFixture fixture)
{
    [Fact]
    public async Task TryCreate_ReturnsNull_WhenTheCodeIsTaken_AndKeepsTheOriginalRow()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUrlRepository>();

        var first = await repository.TryCreateAsync("Dup01", "https://example.com/first");
        var second = await repository.TryCreateAsync("Dup01", "https://example.com/second");

        Assert.NotNull(first);
        Assert.Equal(DateTimeKind.Utc, first.CreatedAt.Kind);
        Assert.Null(second); // INSERT ... ON CONFLICT DO NOTHING: no exception, no overwrite
        Assert.Equal("https://example.com/first", (await repository.GetByCode("Dup01"))?.OriginalUrl);
    }
}
