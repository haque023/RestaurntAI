namespace RestaurantAI.SharedKernel.Tenancy;

/// <summary>
/// The authenticated caller for the current request: User + Tenant + Branch + Roles + Permissions.
/// </summary>
public interface ITenantContext
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    Guid? BranchId { get; }

    /// <summary>The table a QR session is bound to, if any.</summary>
    Guid? TableId => null;

    IReadOnlySet<string> Roles { get; }

    IReadOnlySet<string> Permissions { get; }

    bool IsPlatformAdministrator { get; }

    bool HasPermission(string permission) => IsPlatformAdministrator || Permissions.Contains(permission);
}
