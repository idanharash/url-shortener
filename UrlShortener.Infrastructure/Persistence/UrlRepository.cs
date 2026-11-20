using Npgsql;
using Polly;
using Polly.Registry;
using UrlShortener.Infrastructure.Observability;
using UrlShortener.Model;
using UrlShortener.Model.Repository;

namespace UrlShortener.Infrastructure.Persistence;

/// <summary>
/// Three explicit SQL statements on one table, run through Npgsql directly. Every statement is a single
/// atomic command, so none needs an explicit transaction.
/// </summary>
public class UrlRepository(NpgsqlDataSource dataSource, ResiliencePipelineProvider<string> pipelineProvider) : IUrlRepository
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
            await using var command = dataSource.CreateCommand(
                "SELECT code, original_url, created_at, click_count FROM short_urls WHERE code = @code");
            command.Parameters.AddWithValue("code", code);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            return new ShortUrl
            {
                Code = reader.GetString(0),
                OriginalUrl = reader.GetString(1),
                CreatedAt = AsUtc(reader.GetDateTime(2)),
                ClickCount = reader.GetFieldValue<long>(3)
            };
        }));
    }

    public async Task<ShortUrl?> TryCreateAsync(string code, string originalUrl)
    {
        using var activity = Telemetry.Database.StartActivity("db insert short_url");
        return await Traced(activity, () => Pipeline.ExecuteAsync(async ct =>
        {
            // A collision with another URL returns no row instead of throwing (retrying a constraint violation
            // is pointless). The same code with the same URL returns the stored row: that is this insert already
            // committed by an earlier attempt whose reply was lost, so retrying is safe.
            await using var command = dataSource.CreateCommand(
                """
                INSERT INTO short_urls (code, original_url, click_count, created_at)
                VALUES (@code, @originalUrl, 0, now() AT TIME ZONE 'utc')
                ON CONFLICT (code) DO UPDATE SET original_url = EXCLUDED.original_url
                    WHERE short_urls.original_url = EXCLUDED.original_url
                RETURNING created_at
                """);
            command.Parameters.AddWithValue("code", code);
            command.Parameters.AddWithValue("originalUrl", originalUrl);

            return await command.ExecuteScalarAsync(ct) is DateTime createdAt
                ? new ShortUrl { Code = code, OriginalUrl = originalUrl, CreatedAt = AsUtc(createdAt), ClickCount = 0 }
                : null;
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
                // A single atomic UPDATE: concurrent consumers can never overwrite each other's increments.
                await using var command = dataSource.CreateCommand(
                    "UPDATE short_urls SET click_count = click_count + 1 WHERE code = @code");
                command.Parameters.AddWithValue("code", code);
                await command.ExecuteNonQueryAsync(ctx.CancellationToken);
                return true;
            }, context));
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    // created_at is "timestamp without time zone" holding UTC (written with now() AT TIME ZONE 'utc').
    // Npgsql reads it as DateTimeKind.Unspecified; marking it UTC keeps the API returning "...Z".
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

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
