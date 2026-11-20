using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
        Assert.Null(second); // ON CONFLICT with a different URL: no row, no exception, no overwrite
        Assert.Equal("https://example.com/first", (await repository.GetByCode("Dup01"))?.OriginalUrl);
    }

    [Fact]
    public async Task TryCreate_IsSafeToRetry_WhenTheSameCodeAndUrlAreAlreadyStored()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUrlRepository>();

        // A retry after a commit whose reply was lost: must return the stored row, not report a collision.
        var first = await repository.TryCreateAsync("Rtry1", "https://example.com/retry");
        var retried = await repository.TryCreateAsync("Rtry1", "https://example.com/retry");

        Assert.NotNull(first);
        Assert.NotNull(retried);
        Assert.Equal(first.CreatedAt, retried.CreatedAt);
    }

    [Fact]
    public async Task IncrementClickCount_GoesBeyondTheInt32Range()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUrlRepository>();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        await repository.TryCreateAsync("Big01", "https://example.com/big");
        await using (var command = dataSource.CreateCommand("UPDATE short_urls SET click_count = @count WHERE code = 'Big01'"))
        {
            command.Parameters.AddWithValue("count", (long)int.MaxValue);
            await command.ExecuteNonQueryAsync();
        }

        await repository.IncrementClickCountAsync("Big01");

        // With the original integer column this increment failed with "integer out of range".
        Assert.Equal((long)int.MaxValue + 1, (await repository.GetByCode("Big01"))?.ClickCount);
    }
}
