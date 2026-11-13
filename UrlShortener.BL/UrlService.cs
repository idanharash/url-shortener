using UrlShortener.Model;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.BL;

public class UrlService(
    IUrlRepository repository,
    ICodeGeneratorService codeGenerator,
    ICacheService cache,
    IClickQueueProducer clicks) : IUrlService
{
    // With 62^5 (~916M) possible codes a collision is rare; several in a row means something else is wrong.
    public const int MaxCodeAttempts = 5;

    public async Task<ShortenUrlResponse> CreateShortUrl(string baseUrl, string siteUrl)
    {
        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var created = await repository.TryCreateAsync(codeGenerator.GenerateCode(), siteUrl);
            if (created is null)
                continue; // code already taken: try a new one

            await cache.SetOriginalUrlAsync(created.Code, created.OriginalUrl);
            return new ShortenUrlResponse { Code = created.Code, ShortUrl = $"{baseUrl}/{created.Code}" };
        }

        throw new InvalidOperationException($"Could not generate a unique short code after {MaxCodeAttempts} attempts.");
    }

    public async Task<string?> GetOriginalUrl(string code)
    {
        if (!CodeGeneratorService.IsValidFormat(code))
            return null;

        var originalUrl = await cache.GetOriginalUrlAsync(code);
        if (originalUrl is null)
        {
            var entry = await repository.GetByCode(code);
            if (entry is null)
                return null;

            originalUrl = entry.OriginalUrl;
            await cache.SetOriginalUrlAsync(code, originalUrl);
        }

        clicks.EnqueueClick(code);
        return originalUrl;
    }
}
