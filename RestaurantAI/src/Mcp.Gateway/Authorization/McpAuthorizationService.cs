using ModelContextProtocol;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Context;

namespace RestaurantAI.Mcp.Gateway.Authorization;

/// <summary>
/// Single entry point every tool calls first. Resolves the caller, applies the tool's
/// permission and branch rules, and audits denials. AI cannot bypass normal authorization.
/// </summary>
public sealed class McpAuthorizationService(
    TenantContextResolver resolver,
    ToolPermissionService permissions,
    McpAuditService audit)
{
    public ToolCallerContext Authorize(string toolName, Guid? branchId = null)
    {
        var caller = resolver.Resolve();
        if (caller is null)
        {
            audit.Denied(toolName, null, "Missing or invalid user/tenant context.");
            throw new McpException("Not authorized: a signed-in user with a restaurant context is required.");
        }

        // Fall back to the branch bound to the session (e.g. QR table session).
        var effectiveBranch = branchId ?? caller.Branch.BranchId;
        var result = permissions.Check(toolName, caller, effectiveBranch);
        if (!result.Allowed)
        {
            audit.Denied(toolName, caller, result.Reason!);
            throw new McpException($"Not authorized: {result.Reason}");
        }

        return caller;
    }
}
