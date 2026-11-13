using System.Diagnostics;

namespace UrlShortener.Infrastructure.Observability;

public static class Telemetry
{
    public static readonly ActivitySource Cache = new("UrlShortener.Cache");
    public static readonly ActivitySource Database = new("UrlShortener.Database");
    public static readonly ActivitySource Messaging = new("UrlShortener.Messaging");

    /// <summary>Marks a span as failed so errors are visible in Jaeger.</summary>
    public static void MarkFailed(this Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("exception.type", ex.GetType().FullName);
    }
}
