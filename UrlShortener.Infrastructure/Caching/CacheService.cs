using UrlShortener.Model.Service;
using System.Text.Json;
using StackExchange.Redis;
using UrlShortener.Model;
using UrlShortener.Infrastructure.Observability;
namespace UrlShortener.Infrastructure.Caching
{
    public class CacheService : ICacheService
    {
        private readonly IDatabase _db;
        public CacheService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        private static string GetKey(string code) => $"shorturl:{code}";

        public async Task<ShortUrlCacheEntry?> GetEntryAsync(string code)
        {
            var json = await _db.StringGetAsync(GetKey(code));
            if (!json.HasValue)
            {
                MetricsRegistry.CacheMisses.Inc();
                return null;
            }

            MetricsRegistry.CacheHits.Inc();
            return JsonSerializer.Deserialize<ShortUrlCacheEntry>(json!);
        }

        public async Task SetEntryAsync(string code, ShortUrlCacheEntry entry, TimeSpan? expiry = null)
        {
            var json = JsonSerializer.Serialize(entry);
            await _db.StringSetAsync(GetKey(code), json, expiry);
        }

        public async Task<long?> IncrementClicksAsync(string code)
        {
            var entry = await GetEntryAsync(code);
            if (entry == null)
                return null;

            entry.ClickCount++;
            await SetEntryAsync(code, entry);
            return entry.ClickCount;
        }
    }

}
