using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Infrastructure.Observability;

public sealed class OpenTelemetrySettings
{
    public const string SectionName = "OpenTelemetry";

    [Required] public string ServiceName { get; set; } = string.Empty;
    [Required, Url] public string OtlpEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Also print every span to the console. Debugging aid only: measured at about 4x lower redirect
    /// throughput under load, so it is off by default and deliberately not tied to the Development environment.
    /// </summary>
    public bool ConsoleExporter { get; set; }
}
