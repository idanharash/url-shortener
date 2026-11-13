using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Infrastructure.Caching;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    [Required] public string ConnectionString { get; set; } = string.Empty;
}
