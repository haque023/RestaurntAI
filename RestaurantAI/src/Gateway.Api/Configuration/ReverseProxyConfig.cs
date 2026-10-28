using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using RestaurantAI.SharedKernel.Tenancy;
using Yarp.ReverseProxy.Transforms;

namespace RestaurantAI.Gateway.Api.Configuration;

public sealed class GatewayRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int TenantPermitsPerMinute { get; set; } = 600;

    public int AnonymousPermitsPerMinute { get; set; } = 60;

    public int QueueLimit { get; set; }
}

public static class ReverseProxyConfig
{
    public const string SectionName = "ReverseProxy";

    /// <summary>Route policy: token bucket per tenant (or per client IP for anonymous calls).</summary>
    public const string PerTenantRateLimit = "per-tenant";

    // Headers that only the platform may set; stripped from client requests.
    private static readonly string[] ReservedHeaders = ["X-Tenant-Id", "X-Branch-Id", "X-User-Id"];

    public static IServiceCollection AddGatewayReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

            // Edge-ready: trust the CDN/WAF/load balancer once it is introduced (configure KnownProxies/KnownNetworks).
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection(SectionName))
            .AddTransforms(context =>
            {
                foreach (var header in ReservedHeaders)
                {
                    context.AddRequestHeaderRemove(header);
                }

                context.AddRequestTransform(transform =>
                {
                    transform.ProxyRequest.Headers.Remove(PlatformHeaders.CorrelationId);
                    transform.ProxyRequest.Headers.TryAddWithoutValidation(PlatformHeaders.CorrelationId, transform.HttpContext.TraceIdentifier);
                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }

    /// <summary>
    /// Noisy-neighbour protection: each tenant gets its own token bucket so one tenant cannot
    /// exhaust shared capacity. Plan-based limits can replace the static values later.
    /// </summary>
    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(GatewayRateLimitOptions.SectionName).Get<GatewayRateLimitOptions>() ?? new GatewayRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(PerTenantRateLimit, httpContext =>
            {
                var tenantId = httpContext.User.FindFirstValue(PlatformClaims.TenantId);
                var (key, permits) = tenantId is not null
                    ? ($"tenant:{tenantId}", limits.TenantPermitsPerMinute)
                    : ($"ip:{httpContext.Connection.RemoteIpAddress}", limits.AnonymousPermitsPerMinute);

                return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = permits,
                    TokensPerPeriod = permits,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = limits.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }
}
