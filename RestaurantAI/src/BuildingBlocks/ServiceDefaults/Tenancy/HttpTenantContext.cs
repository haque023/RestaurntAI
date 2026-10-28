using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults.Tenancy;

/// <summary>
/// Resolves tenant context exclusively from validated token claims, never from client-supplied headers.
/// </summary>
internal sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid? UserId => ParseGuid(PlatformClaims.Subject);

    public Guid? TenantId => ParseGuid(PlatformClaims.TenantId);

    public Guid? BranchId => ParseGuid(PlatformClaims.BranchId);

    public IReadOnlySet<string> Roles => User.FindAll(PlatformClaims.Role).Select(c => c.Value).ToHashSet();

    public IReadOnlySet<string> Permissions => User.FindAll(PlatformClaims.Permission).Select(c => c.Value).ToHashSet();

    public bool IsPlatformAdministrator => User.HasClaim(PlatformClaims.Role, PlatformRoles.PlatformAdministrator);

    private Guid? ParseGuid(string claimType) =>
        Guid.TryParse(User.FindFirstValue(claimType), out var value) ? value : null;
}
