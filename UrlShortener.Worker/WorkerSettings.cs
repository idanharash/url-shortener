using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Worker;

public sealed class WorkerSettings
{
    public const string SectionName = "Worker";

    /// <summary>Port of the Worker's Prometheus /metrics endpoint.</summary>
    [Range(1, 65535)] public int MetricsPort { get; set; }
}
