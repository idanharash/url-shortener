using FluentValidation;
using UrlShortener.Application.Dto;

public class UrlRequestDtoValidator : AbstractValidator<UrlRequestDto>
{
    public UrlRequestDtoValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Invalid URL format.");
    }
}