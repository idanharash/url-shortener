using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UrlShortener.Infrastructure.Observability;
using UrlShortener.Model.Service;

namespace UrlShortener.Infrastructure.Caching;

/// <summary>
/// Redis cache for code → original URL. The cache is an optimisation, not a dependency:
/// if Redis is unavailable, reads behave as misses and writes are skipped, so redirects fall back to the database.
/// </summary>
public sealed class CacheService(IConnectionMultiplexer redis, CacheSettings settings, ILogger<CacheService> logger) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    private static RedisKey Key(string code) => $"shorturl:{code}";

    public async Task<string?> GetOriginalUrlAsync(string code)
    {
        using var activity = Telemetry.Cache.StartActivity("cache get");
        try
        {
            var value = await _db.StringGetAsync(Key(code));
            (value.HasValue ? MetricsRegistry.CacheHits : MetricsRegistry.CacheMisses).Inc();
            activity?.SetTag("cache.hit", value.HasValue);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            OnCacheError(activity, ex, code);
            return null;
        }
    }

    public async Task SetOriginalUrlAsync(string code, string originalUrl)
    {
        using var activity = Telemetry.Cache.StartActivity("cache set");
        try
        {
            await _db.StringSetAsync(Key(code), originalUrl, (TimeSpan?)settings.EntryTtl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            OnCacheError(activity, ex, code);
        }
    }

    private void OnCacheError(System.Diagnostics.Activity? activity, Exception ex, string code)
    {
        activity.MarkFailed(ex);
        MetricsRegistry.CacheErrors.Inc();
        logger.LogWarning(ex, "Redis unavailable for code {Code}; falling back to the database", code);
    }
}
