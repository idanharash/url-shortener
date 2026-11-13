namespace UrlShortener.Model.Service;

/// <summary>
/// Caches the code → original URL mapping, which never changes once created.
/// Implementations must not throw when the cache is unavailable: a failed read is a miss.
/// </summary>
public interface ICacheService
{
    Task<string?> GetOriginalUrlAsync(string code);
    Task SetOriginalUrlAsync(string code, string originalUrl);
}
