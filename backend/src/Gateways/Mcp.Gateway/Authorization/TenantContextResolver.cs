using System.Security.Claims;
using RestaurantAI.Mcp.Gateway.Context;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Mcp.Gateway.Authorization;

/// <summary>
/// Builds the caller context strictly from validated token claims. Tenant, branch or user ids
/// proposed by the model in tool arguments are never trusted for scoping.
/// </summary>
public sealed class TenantContextResolver(IHttpContextAccessor accessor)
{
    /// <param name="requireTenant">When false, a caller without a restaurant context resolves with <see cref="Guid.Empty"/> as tenant.</param>
    public ToolCallerContext? Resolve(bool requireTenant = true)
    {
        var http = accessor.HttpContext;
        var user = http?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (!Guid.TryParse(user.FindFirstValue(PlatformClaims.Subject), out var userId))
        {
            return null;
        }

        if (!Guid.TryParse(user.FindFirstValue(PlatformClaims.TenantId), out var tenantId))
        {
            if (requireTenant)
            {
                return null;
            }

            tenantId = Guid.Empty;
        }

        var roles = user.FindAll(PlatformClaims.Role).Select(c => c.Value).ToHashSet();
        var permissions = user.FindAll(PlatformClaims.Permission).Select(c => c.Value).ToHashSet();

        return new ToolCallerContext(
            new UserContext(userId, roles, permissions, roles.Contains(PlatformRoles.PlatformAdministrator)),
            new RestaurantContext(tenantId),
            new BranchContext(ParseGuid(user, PlatformClaims.BranchId), ParseGuid(user, PlatformClaims.TableId)),
            http!.Request.Headers[PlatformHeaders.ConversationId].FirstOrDefault(),
            http.Request.Headers[PlatformHeaders.TurnId].FirstOrDefault());
    }

    private static Guid? ParseGuid(ClaimsPrincipal user, string claimType) =>
        Guid.TryParse(user.FindFirstValue(claimType), out var value) ? value : null;
}
