using Moq;
using UrlShortener.BL;
using UrlShortener.Model;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

public class UrlServiceTests
{
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly Mock<ICodeGeneratorService> _codeGeneratorMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IClickQueueProducer> _clickProducerMock = new();

    private readonly UrlService _service;

    public UrlServiceTests()
    {
        _service = new UrlService(_urlRepositoryMock.Object, _codeGeneratorMock.Object, _cacheServiceMock.Object, _clickProducerMock.Object);
    }

    [Fact]
    public async Task CreateShortUrl_ShouldCreateAndReturnShortUrl()
    {
        // Arrange
        var baseUrl = "http://short.ly";
        var siteUrl = "http://example.com";
        var code = "abc123";
        var createdAt = DateTime.UtcNow;

        _codeGeneratorMock.Setup(c => c.GenerateCode()).Returns(code);
        _urlRepositoryMock.Setup(r => r.CreateShortUrl(siteUrl, code)).ReturnsAsync(new ShortUrl
        {
            Code = code,
            OriginalUrl = siteUrl,
            ClickCount = 0,
            CreatedAt = createdAt
        });

        // Act
        var result = await _service.CreateShortUrl(baseUrl, siteUrl);

        // Assert
        Assert.Equal(code, result.Code);
        Assert.Equal($"{baseUrl}/{code}", result.ShortUrl);

        _cacheServiceMock.Verify(c => c.SetEntryAsync(code,
            It.Is<ShortUrlCacheEntry>(e => e.OriginalUrl == siteUrl && e.ClickCount == 0 && e.CreatedAt == createdAt), null), Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrl_ShouldReturnFromCache_AndSendClick()
    {
        // Arrange
        var code = "abc123";
        var originalUrl = "http://example.com";

        _cacheServiceMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync(new ShortUrlCacheEntry
        {
            OriginalUrl = originalUrl,
            ClickCount = 5,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await _service.GetOriginalUrl(code);

        // Assert
        Assert.Equal(originalUrl, result);
        _clickProducerMock.Verify(p => p.SendClick(code), Times.Once);
        _urlRepositoryMock.Verify(r => r.GetByCode(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOriginalUrl_ShouldFallbackToDb_IfCacheMiss()
    {
        // Arrange
        var code = "abc123";
        var originalUrl = "http://example.com";
        var createdAt = DateTime.UtcNow;

        _cacheServiceMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync((ShortUrlCacheEntry?)null);
        _urlRepositoryMock.Setup(r => r.GetByCode(code)).ReturnsAsync(new ShortUrl
        {
            Code = code,
            OriginalUrl = originalUrl,
            ClickCount = 3,
            CreatedAt = createdAt
        });

        // Act
        var result = await _service.GetOriginalUrl(code);

        // Assert
        Assert.Equal(originalUrl, result);
        _clickProducerMock.Verify(p => p.SendClick(code), Times.Once);
        _cacheServiceMock.Verify(c => c.SetEntryAsync(code,
            It.Is<ShortUrlCacheEntry>(e => e.OriginalUrl == originalUrl && e.ClickCount == 3 && e.CreatedAt == createdAt),null), Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrl_ShouldReturnNull_IfNotFoundAnywhere()
    {
        // Arrange
        var code = "notfound";
        _cacheServiceMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync((ShortUrlCacheEntry?)null);
        _urlRepositoryMock.Setup(r => r.GetByCode(code)).ReturnsAsync((ShortUrl?)null);

        // Act
        var result = await _service.GetOriginalUrl(code);

        // Assert
        Assert.Null(result);
        _clickProducerMock.Verify(p => p.SendClick(It.IsAny<string>()), Times.Never);
        _cacheServiceMock.Verify(c => c.SetEntryAsync(It.IsAny<string>(), It.IsAny<ShortUrlCacheEntry>(),null), Times.Never);
    }
}
