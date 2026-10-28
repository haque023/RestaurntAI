using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace RestaurantAI.Infrastructure.Persistence.PostgreSQL;

public static class PostgresServiceCollectionExtensions
{
    public const string ConnectionStringName = "Platform";

    /// <summary>
    /// Registers a module DbContext on the shared PostgreSQL system of record. Each module owns
    /// its own schema and migrations history table.
    /// </summary>
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null,
        Action<IServiceProvider, DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<TContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema);
                npgsql.EnableRetryOnFailure();
                configureNpgsql?.Invoke(npgsql);
            });
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>());
            configure?.Invoke(provider, options);
        });

        services.AddHealthChecks().AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);

        return services;
    }
}
