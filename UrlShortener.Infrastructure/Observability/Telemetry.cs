using System.Diagnostics;

namespace UrlShortener.Infrastructure.Observability;

public static class Telemetry
{
    public static readonly ActivitySource Messaging = new("UrlShortener.Messaging");
    public static readonly ActivitySource Database = new("UrlShortener.Database");
}
