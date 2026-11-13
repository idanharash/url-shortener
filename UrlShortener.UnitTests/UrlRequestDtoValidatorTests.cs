using FluentValidation.TestHelper;
using UrlShortener.Application.Dto;
using UrlShortener.Application.Settings;
using UrlShortener.Application.Validators;

namespace UrlShortener.UnitTests;

public class UrlRequestDtoValidatorTests
{
    private readonly UrlRequestDtoValidator _validator = new(new AppSettings { PublicBaseUrl = "https://sho.rt" });

    [Theory]
    [InlineData("https://www.example.com")]
    [InlineData("http://example.com/path?q=1#frag")]
    [InlineData("https://sho.rt.example.com/")] // only the service's own host is rejected
    public void Accepts_HttpAndHttpsUrls(string url)
    {
        _validator.TestValidate(new UrlRequestDto { Url = url }).ShouldNotHaveValidationErrorFor(x => x.Url);
    }

    [Theory]
    [InlineData("", "URL is required.")]
    [InlineData("not-a-url", "URL must be an absolute http or https URL.")]
    [InlineData("javascript:alert(1)", "URL must be an absolute http or https URL.")]
    [InlineData("data:text/html,<script>alert(1)</script>", "URL must be an absolute http or https URL.")]
    [InlineData("file:///etc/passwd", "URL must be an absolute http or https URL.")]
    [InlineData("/etc/passwd", "URL must be an absolute http or https URL.")] // absolute file path on Linux
    [InlineData("ftp://example.com/file", "URL must be an absolute http or https URL.")]
    [InlineData("https://sho.rt/abc12", "URL must not point to this service.")]
    [InlineData("HTTPS://SHO.RT/abc12", "URL must not point to this service.")]
    public void Rejects_InvalidOrUnsafeUrls(string url, string expectedError)
    {
        _validator.TestValidate(new UrlRequestDto { Url = url })
            .ShouldHaveValidationErrorFor(x => x.Url)
            .WithErrorMessage(expectedError);
    }

    [Fact]
    public void Rejects_UrlsLongerThanTheLimit()
    {
        var url = "https://example.com/" + new string('a', UrlRequestDtoValidator.MaxUrlLength);

        _validator.TestValidate(new UrlRequestDto { Url = url }).ShouldHaveValidationErrorFor(x => x.Url);
    }
}
