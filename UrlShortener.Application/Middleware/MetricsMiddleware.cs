public class MetricsMiddleware
{
    private static int _requests = 0;
    private static int _errors = 0;

    private readonly RequestDelegate _next;

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        Interlocked.Increment(ref _requests);

        try
        {
            await _next(context);
        }
        catch
        {
            Interlocked.Increment(ref _errors);
            throw;
        }
    }

    public static (int Requests, int Errors) GetMetrics() => (_requests, _errors);
}
