using UrlShortener.Model;
using UrlShortener.Model.Observability;
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
        private readonly IAppTracer _tracer;

        public UrlService(IUrlRepository urlRepository, ICodeGeneratorService codeGeneratorService, ICacheService cacheService, IClickQueueProducer clickQueueProducer,
            IAppTracer tracer)
        {
            _urlRepository = urlRepository;
            _codeGeneratorService = codeGeneratorService;
            _cacheService = cacheService;
            _clickQueueProducer = clickQueueProducer;
            _tracer = tracer;
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
            return await _tracer.TraceAsync("GetOriginalUrl", "App", async activity =>
            {
                activity?.SetTag("url.code", code);

                var cacheEntry = await _tracer.TraceAsync("Cache Lookup", "Cache", _ =>
                    _cacheService.GetEntryAsync(code));

                if (cacheEntry != null)
                {
                    await _tracer.TraceAsync("Send Click (cached)", "Messaging", _ =>
                    {
                        _clickQueueProducer.SendClick(code);
                        return Task.CompletedTask;
                    });

                    return cacheEntry.OriginalUrl;
                }

                var dbEntry = await _tracer.TraceAsync("DB Fetch", "Database", _ =>
                    _urlRepository.GetByCode(code));

                if (dbEntry == null) return null;

                await _tracer.TraceAsync("Send Click (db)", "Messaging", _ =>
                {
                    _clickQueueProducer.SendClick(code);
                    return Task.CompletedTask;
                });

                await _tracer.TraceAsync("Cache Set", "Cache", _ =>
                    _cacheService.SetEntryAsync(code, new ShortUrlCacheEntry
                    {
                        OriginalUrl = dbEntry.OriginalUrl,
                        ClickCount = dbEntry.ClickCount,
                        CreatedAt = dbEntry.CreatedAt
                    }));

                return dbEntry.OriginalUrl;
            });
        }


    }
}
