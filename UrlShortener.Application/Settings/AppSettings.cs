using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Settings;

public sealed class AppSettings
{
    public const string SectionName = "App";

    /// <summary>
    /// Base of the short links returned to clients, e.g. https://sho.rt. Taken from configuration rather than
    /// the request's Host header, which a client can set to anything.
    /// </summary>
    [Required, Url] public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The only port that serves /health and /metrics, so they need not be exposed with the public API.
    /// Kestrel must listen on it too (ASPNETCORE_HTTP_PORTS). Empty: served on every port (local runs, tests).
    /// </summary>
    [Range(1, 65535)] public int? ManagementPort { get; set; }
}
