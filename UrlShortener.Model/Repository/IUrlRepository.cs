namespace UrlShortener.Model.Repository
{
    public interface IUrlRepository
    {
        Task<ShortUrl?> GetByCode(string code);
        Task<ShortUrl> CreateShortUrl(string originalUrl, string code);
        Task IncrementClickCountAsync(string code);
    }
}
