using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;
using RestaurantAI.Mcp.Gateway.Idempotency;

namespace RestaurantAI.Mcp.Gateway.Tools.Orders;

[McpServerToolType]
public sealed class CancelOrderTool(
    McpAuthorizationService authorization,
    McpAuditService audit,
    IdempotencyService idempotency,
    BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.CancelOrder, ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Cancels an order that has not started preparation. Requires explicit customer confirmation.")]
    public Task<string> ExecuteAsync(
        [Description("Order id.")] Guid orderId,
        [Description("Reason given by the customer.")] string reason,
        CancellationToken cancellationToken)
    {
        var caller = authorization.Authorize(McpToolNames.CancelOrder);
        var body = new { reason };

        return audit.TrackAsync(McpToolNames.CancelOrder, caller, () =>
            idempotency.ExecuteAsync(
                McpToolNames.CancelOrder,
                caller,
                new { orderId, reason },
                explicitKey: null,
                (key, ct) => api.PostAsync($"/api/orders/{orderId}/cancel", body, key, ct),
                cancellationToken));
    }
}
