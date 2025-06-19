using Moq;
using UrlShortener.Model;
using UrlShortener.Model.Observability;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;
using UrlShortener.BL;
using System.Diagnostics;

public class UrlServiceTests
{
    private readonly Mock<IUrlRepository> _urlRepoMock = new();
    private readonly Mock<ICodeGeneratorService> _codeGenMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IClickQueueProducer> _clickProducerMock = new();
    private readonly Mock<IAppTracer> _tracerMock = new();

    private readonly UrlService _service;

    public UrlServiceTests()
    {
        _tracerMock.Setup(t => t.TraceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<System.Diagnostics.Activity?, Task<string?>>>()))
            .Returns((string name, string source, Func<System.Diagnostics.Activity?, Task<string?>> fn) => fn(null));

        _tracerMock.Setup(t => t.TraceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<System.Diagnostics.Activity?, Task>>()))
            .Returns((string name, string source, Func<System.Diagnostics.Activity?, Task> fn) => fn(null));

        _service = new UrlService(
            _urlRepoMock.Object,
            _codeGenMock.Object,
            _cacheMock.Object,
            _clickProducerMock.Object,
            _tracerMock.Object);
    }

    [Fact]
    public async Task CreateShortUrl_ShouldCreateAndCacheUrl()
    {
        // Arrange
        var code = "abc123";
        var originalUrl = "https://example.com";
        var baseUrl = "http://short.ly";

        _codeGenMock.Setup(c => c.GenerateCode()).Returns(code);
        _urlRepoMock.Setup(r => r.CreateShortUrl(originalUrl, code))
            .ReturnsAsync(new ShortUrl
            {
                Code = code,
                OriginalUrl = originalUrl,
                ClickCount = 0,
                CreatedAt = DateTime.UtcNow
            });

        // Act
        var result = await _service.CreateShortUrl(baseUrl, originalUrl);

        // Assert
        Assert.Equal(code, result.Code);
        Assert.Equal($"{baseUrl}/{code}", result.ShortUrl);
        _cacheMock.Verify(c => c.SetEntryAsync(code, It.IsAny<ShortUrlCacheEntry>(), null), Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrl_ShouldReturnFromCache_AndSendClick()
    {
        // Arrange
        var code = "abc123";
        var cached = new ShortUrlCacheEntry
        {
            OriginalUrl = "https://cached.com",
            ClickCount = 5,
            CreatedAt = DateTime.UtcNow
        };

        _cacheMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync(cached);

        // Setup for _tracer.TraceAsync<T>
        _tracerMock
            .Setup(t => t.TraceAsync<string?>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Activity?, Task<string?>>>()))
            .Returns((string span, string source, Func<Activity?, Task<string?>> fn) => fn(null!));

        _tracerMock
            .Setup(t => t.TraceAsync<ShortUrlCacheEntry?>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Activity?, Task<ShortUrlCacheEntry?>>>()))
            .Returns((string span, string source, Func<Activity?, Task<ShortUrlCacheEntry?>> fn) => fn(null!));

        _tracerMock
            .Setup(t => t.TraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Activity?, Task>>()))
            .Returns((string span, string source, Func<Activity?, Task> fn) => fn(null!));

        // Act
        var result = await _service.GetOriginalUrl(code);

        // Assert
        Assert.Equal(cached.OriginalUrl, result);
        _clickProducerMock.Verify(p => p.SendClick(code), Times.Once);
        _urlRepoMock.Verify(r => r.GetByCode(It.IsAny<string>()), Times.Never);
    }


    [Fact]
    public async Task GetOriginalUrl_ShouldFetchFromDb_WhenCacheMiss()
    {
        // Arrange
        var code = "abc123";

        _cacheMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync((ShortUrlCacheEntry?)null);

        var dbEntry = new ShortUrl
        {
            Code = code,
            OriginalUrl = "https://fromdb.com",
            ClickCount = 1,
            CreatedAt = DateTime.UtcNow
        };

        _urlRepoMock.Setup(r => r.GetByCode(code)).ReturnsAsync(dbEntry);

        _tracerMock
            .Setup(t => t.TraceAsync<ShortUrl>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Activity?, Task<ShortUrl>>>()))
            .Returns((string span, string source, Func<Activity?, Task<ShortUrl>> fn) => fn(null!));

        _tracerMock
            .Setup(t => t.TraceAsync<string?>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Activity?, Task<string?>>>()))
            .Returns((string span, string source, Func<Activity?, Task<string?>> fn) => fn(null!));

        _tracerMock
            .Setup(t => t.TraceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Activity?, Task>>()))
            .Returns((string span, string source, Func<Activity?, Task> fn) => fn(null!));

        // Act
        var result = await _service.GetOriginalUrl(code);

        // Assert
        Assert.Equal(dbEntry.OriginalUrl, result);
        _clickProducerMock.Verify(p => p.SendClick(code), Times.Once);
        _cacheMock.Verify(c => c.SetEntryAsync(code, It.IsAny<ShortUrlCacheEntry>(), null), Times.Once);
    }


    [Fact]
    public async Task GetOriginalUrl_ShouldReturnNull_WhenCodeNotFound()
    {
        // Arrange
        var code = "notfound";
        _cacheMock.Setup(c => c.GetEntryAsync(code)).ReturnsAsync((ShortUrlCacheEntry?)null);
        _urlRepoMock.Setup(r => r.GetByCode(code)).ReturnsAsync((ShortUrl?)null);

        // Act
        var result = await _service.GetOriginalUrl(code);

        // Assert
        Assert.Null(result);
    }
}
