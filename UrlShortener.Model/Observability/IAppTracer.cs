using System.Diagnostics;

namespace UrlShortener.Model.Observability
{ 
    public interface IAppTracer
    {
        Task<T> TraceAsync<T>(string spanName, string sourceName, Func<Activity?, Task<T>> action);
        Task TraceAsync(string spanName, string sourceName, Func<Activity?, Task> action);
    }
}
