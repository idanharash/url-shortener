using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using UrlShortener.Infrastructure.Caching;

namespace UrlShortener.UnitTests;

public class CacheServiceTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    private readonly Mock<IDatabase> _db = new();
    private readonly CacheService _cache;

    public CacheServiceTests()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        _cache = new CacheService(redis.Object, new CacheSettings { EntryTtl = Ttl }, NullLogger<CacheService>.Instance);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData(null)]
    public async Task GetOriginalUrlAsync_ReturnsTheCachedValueOrNull(string? cached)
    {
        _db.Setup(db => db.StringGetAsync((RedisKey)"shorturl:abc12", It.IsAny<CommandFlags>()))
            .ReturnsAsync(cached is null ? RedisValue.Null : (RedisValue)cached);

        Assert.Equal(cached, await _cache.GetOriginalUrlAsync("abc12"));
    }

    [Fact]
    public async Task SetOriginalUrlAsync_StoresTheMappingWithTheConfiguredTtl()
    {
        await _cache.SetOriginalUrlAsync("abc12", "https://example.com");

        _db.Verify(db => db.StringSetAsync((RedisKey)"shorturl:abc12", (RedisValue)"https://example.com", Ttl,
            false, It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RedisFailures_AreTreatedAsMisses_NotErrors()
    {
        var down = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down");
        _db.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ThrowsAsync(down);
        _db.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ThrowsAsync(down);

        Assert.Null(await _cache.GetOriginalUrlAsync("abc12"));
        await _cache.SetOriginalUrlAsync("abc12", "https://example.com"); // must not throw
    }
}
