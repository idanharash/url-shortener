using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.BL;

public class ClickHandlerService(IUrlRepository repository) : IClickHandlerService
{
    public Task HandleClickAsync(string code) => repository.IncrementClickCountAsync(code);
}
