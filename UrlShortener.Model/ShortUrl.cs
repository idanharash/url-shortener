namespace UrlShortener.Model;

public class ShortUrl
{
    public string Code { get; set; } = null!;
    public string OriginalUrl { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long ClickCount { get; set; }
}
