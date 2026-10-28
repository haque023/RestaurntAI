using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace RestaurantAI.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Development helper: applies pending migrations for a module on startup.
/// Production deployments run migrations from the CI/CD pipeline instead.
/// </summary>
public static class MigrationExtensions
{
    public static async Task MigrateDatabaseAsync<TContext>(this IServiceProvider services, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
