using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.BL
{
    public class ClickHandlerService : IClickHandlerService
    {
        private readonly IUrlRepository _repository;
        private readonly ICacheService _cache;

        public ClickHandlerService(IUrlRepository repository, ICacheService cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task HandleClickAsync(string code)
        {
            await _repository.IncrementClickCountAsync(code);
            await _cache.IncrementClicksAsync(code);
        }
    }
}
