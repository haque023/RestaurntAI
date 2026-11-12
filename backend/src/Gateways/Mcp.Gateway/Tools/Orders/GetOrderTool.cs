using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;

namespace RestaurantAI.Mcp.Gateway.Tools.Orders;

[McpServerToolType]
public sealed class GetOrderTool(McpAuthorizationService authorization, McpAuditService audit, BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.GetOrder, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Returns the live status and contents of an order.")]
    public Task<string> ExecuteAsync([Description("Order id.")] Guid orderId, CancellationToken cancellationToken)
    {
        var caller = authorization.Authorize(McpToolNames.GetOrder);
        return audit.TrackAsync(McpToolNames.GetOrder, caller, () => api.GetAsync($"/api/orders/{orderId}", cancellationToken));
    }
}
