using NHibernate;
using Polly;
using Polly.Registry;
using UrlShortener.Infrastructure.Observability;
using UrlShortener.Model;
using UrlShortener.Model.Repository;

namespace UrlShortener.Infrastructure.Persistence;

public class UrlRepository(ISessionFactory sessionFactory, ResiliencePipelineProvider<string> pipelineProvider) : IUrlRepository
{
    public const string PipelineName = "db-pipeline";

    /// <summary>
    /// Marks an operation the pipeline must not retry: if the commit succeeded but the reply was lost,
    /// a retry would apply the write a second time.
    /// </summary>
    public static readonly ResiliencePropertyKey<bool> NotIdempotent = new("not-idempotent");

    private ResiliencePipeline Pipeline => pipelineProvider.GetPipeline(PipelineName);

    public async Task<ShortUrl?> GetByCode(string code)
    {
        using var activity = Telemetry.Database.StartActivity("db get short_url");
        return await Traced(activity, () => Pipeline.ExecuteAsync(async ct =>
        {
            using var session = sessionFactory.OpenSession();
            return await session.GetAsync<ShortUrl>(code, ct);
        }));
    }

    public async Task<ShortUrl?> TryCreateAsync(string code, string originalUrl)
    {
        using var activity = Telemetry.Database.StartActivity("db insert short_url");
        return await Traced(activity, () => Pipeline.ExecuteAsync(async ct =>
        {
            using var session = sessionFactory.OpenSession();
            using var tx = session.BeginTransaction();

            // A collision with another URL returns no row instead of throwing (an exception would poison the
            // session and be pointlessly retried). The same code with the same URL returns the stored row: that
            // is this insert already committed by an earlier attempt whose reply was lost, so retrying is safe.
            var createdAt = await session.CreateSQLQuery(
                    """
                    INSERT INTO short_urls (code, original_url, click_count, created_at)
                    VALUES (:code, :originalUrl, 0, now() AT TIME ZONE 'utc')
                    ON CONFLICT (code) DO UPDATE SET original_url = EXCLUDED.original_url
                        WHERE short_urls.original_url = EXCLUDED.original_url
                    RETURNING created_at
                    """)
                .AddScalar("created_at", NHibernateUtil.UtcDateTime)
                .SetParameter("code", code)
                .SetParameter("originalUrl", originalUrl)
                .UniqueResultAsync<DateTime?>(ct);

            await tx.CommitAsync(ct);

            return createdAt is null
                ? null
                : new ShortUrl { Code = code, OriginalUrl = originalUrl, CreatedAt = createdAt.Value, ClickCount = 0 };
        }));
    }

    public async Task IncrementClickCountAsync(string code)
    {
        using var activity = Telemetry.Database.StartActivity("db increment click_count");

        // Not idempotent, so no in-process retry (the circuit breaker still applies). A transient failure
        // reaches the consumer, which requeues the message: redelivery is the only retry path.
        var context = ResilienceContextPool.Shared.Get();
        context.Properties.Set(NotIdempotent, true);
        try
        {
            await Traced(activity, () => Pipeline.ExecuteAsync(async ctx =>
            {
                using var session = sessionFactory.OpenSession();
                // A single atomic UPDATE: concurrent consumers can never overwrite each other's increments.
                await session.CreateSQLQuery("UPDATE short_urls SET click_count = click_count + 1 WHERE code = :code")
                    .SetParameter("code", code)
                    .ExecuteUpdateAsync(ctx.CancellationToken);
                return true;
            }, context));
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private static async Task<T> Traced<T>(System.Diagnostics.Activity? activity, Func<ValueTask<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            activity.MarkFailed(ex);
            throw;
        }
    }
}
