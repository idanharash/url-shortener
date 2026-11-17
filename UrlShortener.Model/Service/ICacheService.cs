namespace UrlShortener.Model.Service;

/// <summary>
/// Caches the code → original URL mapping, which never changes once created.
/// Implementations must not throw when the cache is unavailable: a failed read is a miss.
/// </summary>
public interface ICacheService
{
    /// <summary>Returned by <see cref="GetOriginalUrlAsync"/> for a code recently looked up and not found.</summary>
    const string NotFound = "";

    /// <summary>The original URL, <see cref="NotFound"/>, or null on a cache miss.</summary>
    Task<string?> GetOriginalUrlAsync(string code);

    Task SetOriginalUrlAsync(string code, string originalUrl);

    /// <summary>
    /// Briefly remembers that a code does not exist, so repeated guesses don't reach the database.
    /// Never overwrites a stored mapping.
    /// </summary>
    Task SetNotFoundAsync(string code);
}
