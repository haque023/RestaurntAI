using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;

namespace RestaurantAI.Mcp.Gateway.Tools.Menu;

[McpServerToolType]
public sealed class GetMenuTool(McpAuthorizationService authorization, McpAuditService audit, BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.GetMenu, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Returns the current menu for a branch with live prices and availability from the restaurant system.")]
    public Task<string> ExecuteAsync(
        [Description("Branch id. Optional when the session is bound to a branch (QR table session).")] Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var caller = authorization.Authorize(McpToolNames.GetMenu, branchId);
        var branch = branchId ?? caller.Branch.BranchId!.Value;

        return audit.TrackAsync(McpToolNames.GetMenu, caller, () =>
            api.GetAsync($"/api/menu/branches/{branch}/items", cancellationToken));
    }
}
