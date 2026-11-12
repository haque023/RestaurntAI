namespace RestaurantAI.SharedKernel.Domain;

/// <summary>
/// Marks an entity as owned by a tenant (restaurant). Tenant-owned entities are filtered
/// by the application (EF global query filter) and by PostgreSQL Row-Level Security.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}

/// <summary>
/// Marks an entity as scoped to a branch inside a tenant.
/// </summary>
public interface IBranchScoped : ITenantOwned
{
    Guid BranchId { get; }
}
