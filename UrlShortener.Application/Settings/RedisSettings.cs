using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Settings;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    [Required] public string ConnectionString { get; set; } = string.Empty;
}
