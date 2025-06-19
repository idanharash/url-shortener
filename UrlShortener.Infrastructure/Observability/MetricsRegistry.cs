using Prometheus;

namespace UrlShortener.Infrastructure.Observability
{
    public static class MetricsRegistry
    {
        public static readonly Counter CacheHits = Metrics.CreateCounter("url_cache_hits_total", "Number of successful cache hits");
        public static readonly Counter CacheMisses = Metrics.CreateCounter("url_cache_misses_total", "Number of cache misses");
    }
}
