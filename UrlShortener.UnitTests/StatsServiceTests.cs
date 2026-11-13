using Moq;
using UrlShortener.BL;
using UrlShortener.Model;
using UrlShortener.Model.Repository;

namespace UrlShortener.Tests;

public class StatsServiceTests
{
    private readonly Mock<IUrlRepository> _repository = new();
    private readonly StatsService _service;

    public StatsServiceTests()
    {
        _service = new StatsService(_repository.Object);
    }

    [Fact]
    public async Task GetByCode_ReturnsCountsFromTheDatabase()
    {
        var createdAt = DateTime.UtcNow;
        _repository.Setup(r => r.GetByCode("abc12")).ReturnsAsync(new ShortUrl
        {
            Code = "abc12", OriginalUrl = "https://example.com", CreatedAt = createdAt, ClickCount = 12
        });

        var result = await _service.GetByCode("abc12");

        Assert.NotNull(result);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(12, result.ClickCount);
    }

    [Fact]
    public async Task GetByCode_ReturnsNull_ForAnUnknownCode()
    {
        _repository.Setup(r => r.GetByCode("nope1")).ReturnsAsync((ShortUrl?)null);

        Assert.Null(await _service.GetByCode("nope1"));
    }
}
