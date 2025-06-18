using System.Text.Json;
using Moq;
using StackExchange.Redis;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Model;

namespace UrlShortener.Tests
{
    public class CacheServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _connectionMock = new();
        private readonly Mock<IDatabase> _dbMock = new();
        private readonly CacheService _cacheService;

        public CacheServiceTests()
        {
            _connectionMock.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
            _cacheService = new CacheService(_connectionMock.Object);
        }

        [Fact]
        public async Task GetEntryAsync_ShouldReturnNull_WhenKeyMissing()
        {
            // Arrange
            var code = "abc123";
            _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                   .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _cacheService.GetEntryAsync(code);

            // Assert
            Assert.Null(result);
            Assert.True(CacheService.CacheMisses >= 1);
        }

        [Fact]
        public async Task GetEntryAsync_ShouldReturnDeserializedEntry_WhenKeyExists()
        {
            // Arrange
            var code = "abc123";
            var entry = new ShortUrlCacheEntry { OriginalUrl = "http://example.com", ClickCount = 5, CreatedAt = DateTime.UtcNow };
            var json = JsonSerializer.Serialize(entry);

            _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                   .ReturnsAsync(json);

            // Act
            var result = await _cacheService.GetEntryAsync(code);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entry.OriginalUrl, result!.OriginalUrl);
            Assert.Equal(entry.ClickCount, result.ClickCount);
            Assert.True(CacheService.CacheHits >= 1);
        }

        [Fact]
        public async Task SetEntryAsync_ShouldSerializeAndStoreEntry()
        {
            // Arrange
            var code = "abc123";
            var entry = new ShortUrlCacheEntry { OriginalUrl = "http://example.com", ClickCount = 5, CreatedAt = DateTime.UtcNow };
            RedisKey expectedKey = $"shorturl:{code}";
            string expectedJson = JsonSerializer.Serialize(entry);

            _dbMock.Setup(db => db.StringSetAsync(expectedKey, expectedJson, null, It.IsAny<When>(), It.IsAny<CommandFlags>()))
                   .ReturnsAsync(true);

            // Act
            await _cacheService.SetEntryAsync(code, entry);

            // Assert
            _dbMock.Verify(db => db.StringSetAsync(expectedKey, expectedJson, null,false, It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task IncrementClicksAsync_ShouldReturnNull_IfEntryNotFound()
        {
            // Arrange
            var code = "abc123";
            _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                   .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _cacheService.IncrementClicksAsync(code);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task IncrementClicksAsync_ShouldIncrementAndStoreEntry()
        {
            // Arrange
            var code = "abc123";
            var entry = new ShortUrlCacheEntry { OriginalUrl = "http://example.com", ClickCount = 2, CreatedAt = DateTime.UtcNow };
            var originalJson = JsonSerializer.Serialize(entry);

            _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                   .ReturnsAsync(originalJson);

            _dbMock.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, It.IsAny<When>(), It.IsAny<CommandFlags>()))
                   .ReturnsAsync(true);

            // Act
            var result = await _cacheService.IncrementClicksAsync(code);

            // Assert
            Assert.Equal(3, result);
            _dbMock.Verify(db => db.StringSetAsync(It.Is<RedisKey>(k => k.ToString() == $"shorturl:{code}"),
                It.IsAny<RedisValue>(),
                null,false, It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        }
    }

}
