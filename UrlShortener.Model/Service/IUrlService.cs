namespace UrlShortener.Model.Service
{
    public interface IUrlService
    {
        Task<ShortenUrlResponse> CreateShortUrl(string baseUrl, string siteUrl);
        Task<string?> GetOriginalUrl(string code);
    }
}
