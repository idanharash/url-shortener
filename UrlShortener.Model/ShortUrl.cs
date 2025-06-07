namespace UrlShortener.Model
{
    public class ShortUrl
    {
        public virtual string Code { get; set; } = null!;
        public virtual string OriginalUrl { get; set; } = null!;
        public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual long ClickCount { get; set; }
    }
}
