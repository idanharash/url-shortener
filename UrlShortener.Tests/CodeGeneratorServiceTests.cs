using UrlShortener.BL;

public class CodeGeneratorServiceTests
{
    private readonly CodeGeneratorService _service = new();

    [Fact]
    public void GenerateCode_ShouldReturnCodeOfCorrectLength()
    {
        // Act
        var code = _service.GenerateCode();

        // Assert
        Assert.Equal(5, code.Length);
    }

    [Fact]
    public void GenerateCode_ShouldReturnAlphanumericCharactersOnly()
    {
        // Arrange
        const string validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        // Act
        var code = _service.GenerateCode();

        // Assert
        foreach (var c in code)
        {
            Assert.Contains(c, validChars);
        }
    }

    [Fact]
    public void GenerateCode_ShouldNotReturnNullOrEmpty()
    {
        // Act
        var code = _service.GenerateCode();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(code));
    }
}
