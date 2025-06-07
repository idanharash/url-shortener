using UrlShortener.Model;
using UrlShortener.Model.QueueProducer;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.BL
{
    public class UrlService: IUrlService
    {
        private readonly IUrlRepository _urlRepository;
        private readonly ICodeGeneratorService _codeGeneratorService;
        private readonly ICacheService _cacheService;
        private readonly IClickQueueProducer _clickQueueProducer;

        public UrlService(IUrlRepository urlRepository, ICodeGeneratorService codeGeneratorService, ICacheService cacheService, IClickQueueProducer clickQueueProducer)
        {
            _urlRepository = urlRepository;
            _codeGeneratorService = codeGeneratorService;
            _cacheService = cacheService;
            _clickQueueProducer = clickQueueProducer;
        }
        public async Task<ShortenUrlResponse> CreateShortUrl(string baseUrl,string siteUrl)
        {
            var dbEntry = await _urlRepository.CreateShortUrl(siteUrl, _codeGeneratorService.GenerateCode());
            var shortUrl = $"{baseUrl}/{dbEntry.Code}";
            await _cacheService.SetEntryAsync(dbEntry.Code,
                    new ShortUrlCacheEntry() { OriginalUrl = dbEntry.OriginalUrl, ClickCount = dbEntry.ClickCount, CreatedAt = dbEntry.CreatedAt });
            return new ShortenUrlResponse { Code = dbEntry.Code, ShortUrl = shortUrl };
        }

        public async Task<string?> GetOriginalUrl(string code)
        {
            var cacheEntry = await _cacheService.GetEntryAsync(code);
            if (cacheEntry != null)
            {
                _clickQueueProducer.SendClick(code);
                return cacheEntry.OriginalUrl;
            }
            var dbEntry = await _urlRepository.GetByCode(code);
            if(dbEntry  == null) return null;
            _clickQueueProducer.SendClick(code);
            await _cacheService.SetEntryAsync(code, 
                new ShortUrlCacheEntry() { OriginalUrl = dbEntry.OriginalUrl, ClickCount = dbEntry.ClickCount, CreatedAt = dbEntry.CreatedAt });
            return dbEntry?.OriginalUrl;
        }

    }
}
