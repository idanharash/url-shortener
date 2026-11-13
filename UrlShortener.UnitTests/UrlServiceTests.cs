using Moq;
using UrlShortener.BL;
using UrlShortener.Model;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.UnitTests;

public class UrlServiceTests
{
    private const string BaseUrl = "http://short.ly";
    private const string OriginalUrl = "https://example.com/page";

    private readonly Mock<IUrlRepository> _repository = new();
    private readonly Mock<ICodeGeneratorService> _codes = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IClickQueueProducer> _clicks = new();
    private readonly UrlService _service;

    public UrlServiceTests()
    {
        _service = new UrlService(_repository.Object, _codes.Object, _cache.Object, _clicks.Object);
    }

    [Fact]
    public async Task CreateShortUrl_StoresAndCachesTheMapping()
    {
        _codes.Setup(c => c.GenerateCode()).Returns("abc12");
        _repository.Setup(r => r.TryCreateAsync("abc12", OriginalUrl)).ReturnsAsync(Stored("abc12"));

        var result = await _service.CreateShortUrl(BaseUrl, OriginalUrl);

        Assert.Equal("abc12", result.Code);
        Assert.Equal($"{BaseUrl}/abc12", result.ShortUrl);
        _cache.Verify(c => c.SetOriginalUrlAsync("abc12", OriginalUrl), Times.Once);
    }

    [Fact]
    public async Task CreateShortUrl_TriesANewCode_WhenTheCodeIsTaken()
    {
        _codes.SetupSequence(c => c.GenerateCode()).Returns("taken").Returns("fresh");
        _repository.Setup(r => r.TryCreateAsync("taken", OriginalUrl)).ReturnsAsync((ShortUrl?)null);
        _repository.Setup(r => r.TryCreateAsync("fresh", OriginalUrl)).ReturnsAsync(Stored("fresh"));

        var result = await _service.CreateShortUrl(BaseUrl, OriginalUrl);

        Assert.Equal("fresh", result.Code);
    }

    [Fact]
    public async Task CreateShortUrl_GivesUp_AfterMaxAttempts()
    {
        _codes.Setup(c => c.GenerateCode()).Returns("taken");
        _repository.Setup(r => r.TryCreateAsync(It.IsAny<string>(), OriginalUrl)).ReturnsAsync((ShortUrl?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateShortUrl(BaseUrl, OriginalUrl));

        _repository.Verify(r => r.TryCreateAsync(It.IsAny<string>(), OriginalUrl), Times.Exactly(UrlService.MaxCodeAttempts));
        _cache.Verify(c => c.SetOriginalUrlAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOriginalUrl_CacheHit_SkipsTheDatabaseAndRecordsTheClick()
    {
        _cache.Setup(c => c.GetOriginalUrlAsync("abc12")).ReturnsAsync(OriginalUrl);

        var result = await _service.GetOriginalUrl("abc12");

        Assert.Equal(OriginalUrl, result);
        _repository.Verify(r => r.GetByCode(It.IsAny<string>()), Times.Never);
        _clicks.Verify(c => c.EnqueueClick("abc12"), Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrl_CacheMiss_LoadsFromTheDatabaseCachesAndRecordsTheClick()
    {
        _cache.Setup(c => c.GetOriginalUrlAsync("abc12")).ReturnsAsync((string?)null);
        _repository.Setup(r => r.GetByCode("abc12")).ReturnsAsync(Stored("abc12"));

        var result = await _service.GetOriginalUrl("abc12");

        Assert.Equal(OriginalUrl, result);
        _cache.Verify(c => c.SetOriginalUrlAsync("abc12", OriginalUrl), Times.Once);
        _clicks.Verify(c => c.EnqueueClick("abc12"), Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrl_UnknownCode_ReturnsNullAndRecordsNoClick()
    {
        _cache.Setup(c => c.GetOriginalUrlAsync("nope1")).ReturnsAsync((string?)null);
        _repository.Setup(r => r.GetByCode("nope1")).ReturnsAsync((ShortUrl?)null);

        var result = await _service.GetOriginalUrl("nope1");

        Assert.Null(result);
        _clicks.Verify(c => c.EnqueueClick(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abc123")]
    [InlineData("ab-12")]
    [InlineData("favicon.ico")]
    public async Task GetOriginalUrl_MalformedCode_ReturnsNullWithoutTouchingCacheOrDatabase(string code)
    {
        Assert.Null(await _service.GetOriginalUrl(code));

        _cache.Verify(c => c.GetOriginalUrlAsync(It.IsAny<string>()), Times.Never);
        _repository.Verify(r => r.GetByCode(It.IsAny<string>()), Times.Never);
    }

    private static ShortUrl Stored(string code) =>
        new() { Code = code, OriginalUrl = OriginalUrl, CreatedAt = DateTime.UtcNow, ClickCount = 0 };
}
