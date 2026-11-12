using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RestaurantAI.Infrastructure.Persistence.Redis;

/// <summary>
/// Redis is used for caching, short-lived session state, rate limiting and distributed locks.
/// It is never the authoritative store for transactional records.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    public const string ConnectionStringName = "Redis";

    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration, string instanceName)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Allows services and tests to run without Redis; IDistributedCache falls back to memory.
            services.AddDistributedMemoryCache();
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = $"{instanceName}:";
        });

        return services;
    }
}
