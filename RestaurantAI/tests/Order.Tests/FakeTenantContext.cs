using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Tests;

internal sealed class FakeTenantContext : ITenantContext
{
    public Guid? UserId { get; init; } = Guid.NewGuid();

    public Guid? TenantId { get; init; } = Guid.NewGuid();

    public Guid? BranchId { get; init; }

    public IReadOnlySet<string> Roles { get; init; } = new HashSet<string>();

    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>();

    public bool IsPlatformAdministrator { get; init; }
}
