using Moq;
using UrlShortener.BL;
using UrlShortener.Model.Repository;

namespace UrlShortener.UnitTests;

public class ClickHandlerServiceTests
{
    [Fact]
    public async Task HandleClickAsync_IncrementsTheStoredCount()
    {
        var repository = new Mock<IUrlRepository>();

        await new ClickHandlerService(repository.Object).HandleClickAsync("abc12");

        repository.Verify(r => r.IncrementClickCountAsync("abc12"), Times.Once);
    }
}
