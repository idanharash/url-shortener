using UrlShortener.BL;

namespace UrlShortener.Tests;

public class CodeGeneratorServiceTests
{
    [Fact]
    public void GenerateCode_ReturnsFixedLengthAlphanumericCodes()
    {
        var generator = new CodeGeneratorService();

        for (var i = 0; i < 1_000; i++)
        {
            var code = generator.GenerateCode();
            Assert.Equal(CodeGeneratorService.CodeLength, code.Length);
            Assert.All(code, c => Assert.Contains(c, CodeGeneratorService.Alphabet));
        }
    }
}
