namespace UrlShortener.Model.Service
{
    public interface ICacheService
    {
        Task<ShortUrlCacheEntry?> GetEntryAsync(string code);
        Task SetEntryAsync(string code, ShortUrlCacheEntry entry, TimeSpan? expiry = null);
        Task<long?> IncrementClicksAsync(string code);
    }
}
