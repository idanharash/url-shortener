using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Security;

public sealed class ApiKeySettings : IValidatableObject
{
    public const string SectionName = "Auth";
    public const int MinimumKeyLength = 32;

    /// <summary>
    /// Keys allowed to create links. More than one is allowed so a key can be rotated without downtime:
    /// add the new key, move clients over, then remove the old one.
    /// </summary>
    public IReadOnlyList<string> ApiKeys { get; set; } = [];

    // Fails closed: with no usable key the app refuses to start, so the endpoint can never be left unprotected.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ApiKeys.Count == 0 || ApiKeys.Any(key => string.IsNullOrWhiteSpace(key) || key.Length < MinimumKeyLength))
            yield return new ValidationResult(
                $"At least one API key is required, each with at least {MinimumKeyLength} characters.",
                [nameof(ApiKeys)]);
    }
}
