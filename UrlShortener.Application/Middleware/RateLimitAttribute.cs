namespace UrlShortener.Application.Middleware;

public enum RateLimitPolicy
{
    /// <summary>Link creation: <c>RateLimiting:PermitLimit</c> per <c>Window</c>.</summary>
    Shorten,

    /// <summary>Redirects and stats: <c>RateLimiting:ReadPermitLimit</c> per <c>ReadWindow</c>.</summary>
    Read
}

/// <summary>Applies a <see cref="RateLimitingMiddleware"/> policy to a controller or action.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RateLimitAttribute(RateLimitPolicy policy) : Attribute
{
    public RateLimitPolicy Policy { get; } = policy;
}
