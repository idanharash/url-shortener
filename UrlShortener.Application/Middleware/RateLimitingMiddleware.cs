using StackExchange.Redis;
using System.Net;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    private const int MAX_REQUESTS = 20;
    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    public RateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redisConnection, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _redis = redisConnection.GetDatabase();
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"rl:{ip}";

        var count = await _redis.StringIncrementAsync(key);

        if (count == 1)
            await _redis.KeyExpireAsync(key, WINDOW);
        

        if (count > MAX_REQUESTS)
        {
            _logger.LogWarning("Rate limit exceeded for IP: {IP}", ip);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
            return;
        }

        await _next(context);
    }
}
