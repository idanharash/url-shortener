using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Settings;

public sealed class OpenTelemetrySettings
{
    public const string SectionName = "OpenTelemetry";

    [Required] public string ServiceName { get; set; } = string.Empty;
    [Required, Url] public string OtlpEndpoint { get; set; } = string.Empty;
}
