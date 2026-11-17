using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Infrastructure.Caching;

public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>How long a code → URL mapping stays in Redis. Bounds memory; mappings never change, so it can be long.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "30.00:00:00")]
    public TimeSpan EntryTtl { get; set; }

    /// <summary>How long an unknown code is remembered as not found. Short, so it never hides a link for long.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan NotFoundTtl { get; set; }
}
