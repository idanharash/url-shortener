using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Settings;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Shorten policy: maximum POST /api/shorten requests per client IP within <see cref="Window"/>.</summary>
    [Range(1, 100_000)] public int PermitLimit { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Window { get; set; }

    /// <summary>Read policy: maximum redirect and stats requests per client IP within <see cref="ReadWindow"/>.</summary>
    [Range(1, 10_000_000)] public int ReadPermitLimit { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan ReadWindow { get; set; }
}
