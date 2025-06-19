using FluentValidation.TestHelper;
using UrlShortener.Application.Dto;

public class UrlRequestDtoValidatorTests
{
    private readonly UrlRequestDtoValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Url_Is_Empty()
    {
        var model = new UrlRequestDto { Url = "" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Url)
              .WithErrorMessage("URL is required.");
    }

    [Fact]
    public void Should_Have_Error_When_Url_Is_Invalid()
    {
        var model = new UrlRequestDto { Url = "not-a-url" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Url)
              .WithErrorMessage("Invalid URL format.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Url_Is_Valid()
    {
        var model = new UrlRequestDto { Url = "https://www.example.com" };
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Url);
    }
}
