using System.Diagnostics;
using UrlShortener.Model.Observability;

public static class Telemetry
{
    public static readonly ActivitySource App = new("UrlShortener.Application");
    public static readonly ActivitySource Cache = new("UrlShortener.Cache");
    public static readonly ActivitySource Database = new("UrlShortener.Database");
    public static readonly ActivitySource Messaging = new("UrlShortener.Messaging");
}
public class AppTracer : IAppTracer
{
    private readonly Dictionary<string, ActivitySource> _sources = new()
    {
        ["App"] = Telemetry.App,
        ["Cache"] = Telemetry.Cache,
        ["Database"] = Telemetry.Database,
        ["Messaging"] = Telemetry.Messaging
    };

    public async Task<T> TraceAsync<T>(string spanName, string sourceName, Func<Activity?, Task<T>> action)
    {
        var source = _sources[sourceName];
        using var activity = source.StartActivity(spanName, ActivityKind.Internal);

        // הכי חשוב: מוודא שה־Activity נכנס ל־Activity.Current
        if (activity != null) Activity.Current = activity;

        return await action(activity);
    }

    public async Task TraceAsync(string spanName, string sourceName, Func<Activity?, Task> action)
    {
        var source = _sources[sourceName];
        using var activity = source.StartActivity(spanName, ActivityKind.Internal);

        if (activity != null) Activity.Current = activity;

        await action(activity);
    }
}


