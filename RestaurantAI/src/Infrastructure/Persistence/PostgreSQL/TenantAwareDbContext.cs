using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Domain;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Base DbContext for every module. Applies the application-level tenant filter (layer 3);
/// PostgreSQL Row-Level Security provides the database-level boundary (layer 4).
/// </summary>
public abstract class TenantAwareDbContext(DbContextOptions options, ITenantContext tenantContext) : DbContext(options)
{
    public const string TenantFilter = "Tenant";

    protected ITenantContext TenantContext { get; } = tenantContext;

    /// <summary>The PostgreSQL schema owned by this module.</summary>
    protected abstract string Schema { get; }

    // Evaluated per query by EF Core, so each request sees its own tenant.
    private Guid? CurrentTenantId => TenantContext.TenantId;

    private bool BypassTenantFilter => TenantContext.IsPlatformAdministrator;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Ids are assigned by the domain (Guid v7), never by the database. Without this EF treats a
        // new child entity with a non-empty key (e.g. a Branch added to a loaded Restaurant) as
        // existing and issues an UPDATE instead of an INSERT.
        foreach (var key in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetKeys()).Where(k => k.IsPrimaryKey()))
        {
            foreach (var property in key.Properties.Where(p => p.ClrType == typeof(Guid)))
            {
                property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private static readonly System.Reflection.MethodInfo ApplyTenantFilterMethod =
        typeof(TenantAwareDbContext).GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        Expression<Func<TEntity, bool>> filter = entity => BypassTenantFilter || entity.TenantId == CurrentTenantId;
        modelBuilder.Entity<TEntity>().HasQueryFilter(TenantFilter, filter);
    }
}
