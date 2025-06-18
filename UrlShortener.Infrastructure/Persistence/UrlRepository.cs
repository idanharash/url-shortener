using NHibernate;
using Polly.Registry;
using Polly;
using UrlShortener.Model;
using UrlShortener.Model.Repository;

public class UrlRepository : IUrlRepository
{
    private readonly ISessionFactory _sessionFactory;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public UrlRepository(ISessionFactory sessionFactory, ResiliencePipelineProvider<string> pipelineProvider)
    {
        _sessionFactory = sessionFactory;
        _pipelineProvider = pipelineProvider;
    }

    private ResiliencePipeline GetPipeline() => _pipelineProvider.GetPipeline("db-pipeline");

    public async Task<ShortUrl?> GetByCode(string code)
    {
        return await GetPipeline().ExecuteAsync(async _ =>
        {
            using var session = _sessionFactory.OpenSession();
            return await session.GetAsync<ShortUrl>(code);
        });
    }

    public async Task<ShortUrl> CreateShortUrl(string originalUrl, string code)
    {
        return await GetPipeline().ExecuteAsync(async _ =>
        {
            using var session = _sessionFactory.OpenSession();
            using var tx = session.BeginTransaction();

            var entity = new ShortUrl
            {
                Code = code,
                OriginalUrl = originalUrl,
                CreatedAt = DateTime.UtcNow
            };

            await session.SaveAsync(entity);
            await tx.CommitAsync();
            return entity;
        });
    }

    public async Task IncrementClickCountAsync(string code)
    {
        await GetPipeline().ExecuteAsync(async _ =>
        {
            using var session = _sessionFactory.OpenSession();
            using var tx = session.BeginTransaction();

            var url = await session.GetAsync<ShortUrl>(code);
            if (url != null)
            {
                url.ClickCount += 1;
                await session.UpdateAsync(url);
                await tx.CommitAsync();
            }
        });
    }
}
