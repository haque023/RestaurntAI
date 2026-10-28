using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;

namespace RestaurantAI.Mcp.Gateway.Tools.Menu;

[McpServerToolType]
public sealed class GetAvailabilityTool(McpAuthorizationService authorization, McpAuditService audit, BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.GetAvailability, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Checks live availability and current price of a menu item. Always use this instead of remembered or retrieved prices.")]
    public Task<string> ExecuteAsync(
        [Description("Menu item id.")] Guid menuItemId,
        CancellationToken cancellationToken)
    {
        var caller = authorization.Authorize(McpToolNames.GetAvailability);
        return audit.TrackAsync(McpToolNames.GetAvailability, caller, () =>
            api.GetAsync($"/api/menu/items/{menuItemId}/availability", cancellationToken));
    }
}
