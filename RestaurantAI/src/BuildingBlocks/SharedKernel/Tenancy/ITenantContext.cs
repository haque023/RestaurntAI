namespace RestaurantAI.SharedKernel.Tenancy;

/// <summary>
/// The authenticated caller for the current request: User + Tenant + Branch + Roles + Permissions.
/// </summary>
public interface ITenantContext
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    Guid? BranchId { get; }

    IReadOnlySet<string> Roles { get; }

    IReadOnlySet<string> Permissions { get; }

    bool IsPlatformAdministrator { get; }

    bool HasPermission(string permission) => IsPlatformAdministrator || Permissions.Contains(permission);
}
