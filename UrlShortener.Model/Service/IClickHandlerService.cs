namespace UrlShortener.Model.Service
{
    public interface IClickHandlerService
    {
        Task HandleClickAsync(string code);
    }
}
