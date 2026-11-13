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
}
