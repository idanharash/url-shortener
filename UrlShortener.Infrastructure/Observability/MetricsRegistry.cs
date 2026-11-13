using Prometheus;

namespace UrlShortener.Infrastructure.Observability;

public static class MetricsRegistry
{
    public static readonly Counter CacheHits = Metrics.CreateCounter("url_cache_hits_total", "Number of successful cache hits");
    public static readonly Counter CacheMisses = Metrics.CreateCounter("url_cache_misses_total", "Number of cache misses");
    public static readonly Counter CacheErrors = Metrics.CreateCounter("url_cache_errors_total", "Redis calls that failed and fell back to the database");

    public static readonly Counter ClicksPublished = Metrics.CreateCounter("url_clicks_published_total", "Click events published to RabbitMQ");
    public static readonly Counter ClicksDropped = Metrics.CreateCounter("url_clicks_dropped_total", "Click events dropped because the buffer was full or RabbitMQ was unavailable");
    public static readonly Counter ClicksProcessed = Metrics.CreateCounter("url_clicks_processed_total", "Click events stored in the database");
    public static readonly Counter ClicksDeadLettered = Metrics.CreateCounter("url_clicks_dead_lettered_total", "Click events moved to the dead-letter queue");
}
