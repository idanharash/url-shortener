using System.Globalization;
using StackExchange.Redis;
using UrlShortener.Application.Settings;

namespace UrlShortener.Application.Middleware;

/// <summary>
/// Fixed-window rate limit per client IP, stored in Redis so it holds across several app instances.
/// Fails open: if Redis is unavailable, requests are allowed. Rate limiting protects against abuse,
/// and blocking every user because the limiter's store is down would be worse.
/// </summary>
public class RateLimitingMiddleware(
    RequestDelegate next,
    IConnectionMultiplexer redis,
    RateLimitingSettings settings,
    ILogger<RateLimitingMiddleware> logger)
{
    // INCR and PEXPIRE in one atomic script: with two separate calls, a crash in between would leave a key
    // without expiry and block that IP permanently. Returns { count, remaining window in ms }.
    private static readonly LuaScript IncrementScript = LuaScript.Prepare(
        """
        local count = redis.call('INCR', @key)
        if count == 1 then redis.call('PEXPIRE', @key, @windowMs) end
        return { count, redis.call('PTTL', @key) }
        """);

    private readonly IDatabase _db = redis.GetDatabase();

    public async Task InvokeAsync(HttpContext context)
    {
        // Behind a reverse proxy this is the proxy's address; see README "Known limitations".
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        long count, remainingMs;
        try
        {
            var raw = await _db.ScriptEvaluateAsync(IncrementScript, new
            {
                key = (RedisKey)$"rl:shorten:{ip}",
                windowMs = (long)settings.Window.TotalMilliseconds
            });
            var result = ((RedisResult[]?)raw)!;
            count = (long)result[0];
            remainingMs = (long)result[1];
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Rate limiter unavailable (Redis); allowing the request");
            await next(context);
            return;
        }

        if (count > settings.PermitLimit)
        {
            logger.LogWarning("Rate limit exceeded for {IP}", ip);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = Math.Max(1, (long)Math.Ceiling(remainingMs / 1000.0)).ToString(CultureInfo.InvariantCulture);
            await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
            return;
        }

        await next(context);
    }
}
