using System.Diagnostics;

namespace UrlShortener.Infrastructure.Observability;

public static class Telemetry
{
    public static readonly ActivitySource App = new("UrlShortener.Application");
    public static readonly ActivitySource Cache = new("UrlShortener.Cache");
    public static readonly ActivitySource Database = new("UrlShortener.Database");
    public static readonly ActivitySource Messaging = new("UrlShortener.Messaging");
}
