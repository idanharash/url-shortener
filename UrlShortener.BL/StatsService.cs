using UrlShortener.Model.Repository;
using UrlShortener.Model;
using UrlShortener.Model.Service;

namespace UrlShortener.BL
{
    public class StatsService: IStatsService
    {
        private readonly IUrlRepository _urlRepository;
        private readonly ICacheService _cacheService;

        public StatsService(IUrlRepository urlRepository, ICacheService cacheService)
        {
            _urlRepository = urlRepository;
            _cacheService = cacheService;
        }
        public async Task<GetStatsByCodeResponse?> GetByCode(string code)
        {
            var cacheEntry = await _cacheService.GetEntryAsync(code);
            if (cacheEntry != null) return new GetStatsByCodeResponse() { CreatedAt = cacheEntry.CreatedAt, ClickCount = cacheEntry.ClickCount };
            var dbEntry = await _urlRepository.GetByCode(code);
            if(dbEntry == null) return null;
            await _cacheService.SetEntryAsync(code, new ShortUrlCacheEntry() { OriginalUrl = dbEntry.OriginalUrl, ClickCount = dbEntry.ClickCount, CreatedAt = dbEntry.CreatedAt });
            return new GetStatsByCodeResponse() { CreatedAt = dbEntry.CreatedAt, ClickCount = dbEntry.ClickCount };
        }
    }
}
