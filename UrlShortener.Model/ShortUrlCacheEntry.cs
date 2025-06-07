namespace UrlShortener.Model
{
    public class ShortUrlCacheEntry
    {
        public string OriginalUrl { get; set; } = null!;
        public long ClickCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
