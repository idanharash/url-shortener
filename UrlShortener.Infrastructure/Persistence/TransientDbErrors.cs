using Npgsql;

namespace UrlShortener.Infrastructure.Persistence;

public static class TransientDbErrors
{
    /// <summary>
    /// True for errors worth retrying (connection drops, timeouts). Constraint violations, SQL errors
    /// and mapping bugs are not retried: retrying them only adds latency before the same failure.
    /// NHibernate wraps provider exceptions, so inner exceptions are checked too.
    /// </summary>
    public static bool IsTransient(Exception ex) => ex switch
    {
        NpgsqlException npgsql => npgsql.IsTransient,
        TimeoutException => true,
        { InnerException: { } inner } => IsTransient(inner),
        _ => false
    };
}
