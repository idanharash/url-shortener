using FluentValidation;
using UrlShortener.Application.Dto;
using UrlShortener.Application.Settings;

namespace UrlShortener.Application.Validators;

public class UrlRequestDtoValidator : AbstractValidator<UrlRequestDto>
{
    // Common practical limit for URLs across browsers and servers.
    public const int MaxUrlLength = 2048;

    public UrlRequestDtoValidator(AppSettings settings)
    {
        var publicBaseUri = new Uri(settings.PublicBaseUrl);

        RuleFor(x => x.Url)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("URL is required.")
            .MaximumLength(MaxUrlLength).WithMessage($"URL must be at most {MaxUrlLength} characters.")
            // Only http(s): javascript:, data: or file: targets would turn the service into an attack vector.
            .Must(BeHttpOrHttpsUrl).WithMessage("URL must be an absolute http or https URL.")
            // Shortening our own links creates redirect chains and loops.
            .Must(url => !PointsTo(url, publicBaseUri)).WithMessage("URL must not point to this service.");
    }

    private static bool BeHttpOrHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    private static bool PointsTo(string url, Uri publicBaseUri) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && Uri.Compare(uri, publicBaseUri, UriComponents.HostAndPort, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
}
