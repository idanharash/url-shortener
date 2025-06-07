using Moq;
using UrlShortener.BL;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;
using UrlShortener.Model;

public class StatsServiceTests
{
    private readonly Mock<IUrlRepository> _repositoryMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly StatsService _service;

    public StatsServiceTests()
    {
        _service = new StatsService(_repositoryMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task GetByCode_ShouldReturnFromCache_WhenExists()
    {
        // Arrange
        var code = "abc123";
        var createdAt = DateTime.UtcNow;
        var clickCount = 5;

        _cacheMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync(new ShortUrlCacheEntry
        {
            CreatedAt = createdAt,
            ClickCount = clickCount
        });

        // Act
        var result = await _service.GetByCode(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createdAt, result!.CreatedAt);
        Assert.Equal(clickCount, result.ClickCount);
        _repositoryMock.Verify(r => r.GetByCode(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetByCode_ShouldFallbackToDb_WhenCacheMiss()
    {
        // Arrange
        var code = "abc123";
        var createdAt = DateTime.UtcNow;
        var clickCount = 12;
        var originalUrl = "http://example.com";

        _cacheMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync((ShortUrlCacheEntry?)null);
        _repositoryMock.Setup(r => r.GetByCode(code)).ReturnsAsync(new ShortUrl
        {
            Code = code,
            CreatedAt = createdAt,
            ClickCount = clickCount,
            OriginalUrl = originalUrl
        });

        // Act
        var result = await _service.GetByCode(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createdAt, result!.CreatedAt);
        Assert.Equal(clickCount, result.ClickCount);

        _cacheMock.Verify(c => c.SetEntryAsync(code, It.Is<ShortUrlCacheEntry>(e =>
            e.OriginalUrl == originalUrl &&
            e.ClickCount == clickCount &&
            e.CreatedAt == createdAt),null), Times.Once);
    }

    [Fact]
    public async Task GetByCode_ShouldReturnNull_WhenNotInCacheOrDb()
    {
        // Arrange
        var code = "notfound";

        _cacheMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync((ShortUrlCacheEntry?)null);
        _repositoryMock.Setup(r => r.GetByCode(code)).ReturnsAsync((ShortUrl?)null);

        // Act
        var result = await _service.GetByCode(code);

        // Assert
        Assert.Null(result);
        _cacheMock.Verify(c => c.SetEntryAsync(It.IsAny<string>(), It.IsAny<ShortUrlCacheEntry>(),null), Times.Never);
    }
}
