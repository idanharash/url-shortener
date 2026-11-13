using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Settings;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Maximum POST /api/shorten requests per client IP within one window.</summary>
    [Range(1, 100_000)] public int PermitLimit { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Window { get; set; }
}
