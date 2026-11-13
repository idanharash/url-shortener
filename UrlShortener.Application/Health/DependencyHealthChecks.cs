using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using StackExchange.Redis;
using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Application.Health;

// Statuses reflect how much each dependency matters: without PostgreSQL nothing works (Unhealthy → 503),
// while Redis and RabbitMQ outages only degrade the service (redirects still work).

public sealed class PostgresHealthCheck(PostgresSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(settings.ToConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable", ex);
        }
    }
}

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Redis is unreachable; redirects fall back to PostgreSQL", ex);
        }
    }
}

// The API only publishes clicks; whether the Worker is consuming them shows in its own logs and metrics.
public sealed class RabbitMqHealthCheck(ClickQueueProducer producer) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(producer.IsConnected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded("RabbitMQ is unreachable; clicks are buffered in memory and counts are delayed"));
}
