using Moq;
using UrlShortener.BL;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

public class ClickHandlerServiceTests
{
    private readonly Mock<IUrlRepository> _repositoryMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly ClickHandlerService _service;

    public ClickHandlerServiceTests()
    {
        _service = new ClickHandlerService(_repositoryMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task HandleClickAsync_ShouldCallRepositoryAndCache()
    {
        // Arrange
        var code = "abc123";

        _repositoryMock.Setup(r => r.IncrementClickCountAsync(code)).Returns(Task.CompletedTask);
        _cacheMock.Setup(c => c.IncrementClicksAsync(code)).ReturnsAsync(42);

        // Act
        await _service.HandleClickAsync(code);

        // Assert
        _repositoryMock.Verify(r => r.IncrementClickCountAsync(code), Times.Once);
        _cacheMock.Verify(c => c.IncrementClicksAsync(code), Times.Once);
    }

    [Fact]
    public async Task HandleClickAsync_ShouldNotThrow_WhenCacheReturnsNull()
    {
        // Arrange
        var code = "abc123";

        _repositoryMock.Setup(r => r.IncrementClickCountAsync(code)).Returns(Task.CompletedTask);
        _cacheMock.Setup(c => c.IncrementClicksAsync(code)).ReturnsAsync((long?)null);

        // Act
        var exception = await Record.ExceptionAsync(() => _service.HandleClickAsync(code));

        // Assert
        Assert.Null(exception);
        _repositoryMock.Verify(r => r.IncrementClickCountAsync(code), Times.Once);
        _cacheMock.Verify(c => c.IncrementClicksAsync(code), Times.Once);
    }
}
