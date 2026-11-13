using UrlShortener.Model;
using UrlShortener.Model.Repository;
using UrlShortener.Model.Service;

namespace UrlShortener.BL;

/// <summary>
/// Reads stats from the database, the single source of truth for click counts.
/// Counts are eventually consistent: clicks still in the queue are not included yet.
/// </summary>
public class StatsService(IUrlRepository repository) : IStatsService
{
    public async Task<GetStatsByCodeResponse?> GetByCode(string code)
    {
        if (!CodeGeneratorService.IsValidFormat(code))
            return null;

        var entry = await repository.GetByCode(code);
        return entry is null
            ? null
            : new GetStatsByCodeResponse { CreatedAt = entry.CreatedAt, ClickCount = entry.ClickCount };
    }
}
