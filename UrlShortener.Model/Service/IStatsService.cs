namespace UrlShortener.Model.Service
{
    public interface IStatsService
    {
        Task<GetStatsByCodeResponse?> GetByCode(string code);
    }
}
