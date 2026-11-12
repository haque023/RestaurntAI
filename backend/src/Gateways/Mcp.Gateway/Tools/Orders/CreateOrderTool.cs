using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;
using RestaurantAI.Mcp.Gateway.Idempotency;

namespace RestaurantAI.Mcp.Gateway.Tools.Orders;

public sealed record OrderItemInput(
    [property: Description("Menu item id.")] Guid MenuItemId,
    [property: Description("Quantity (1-99).")] int Quantity,
    [property: Description("Optional preparation notes.")] string? Notes);

[McpServerToolType]
public sealed class CreateOrderTool(
    McpAuthorizationService authorization,
    McpAuditService audit,
    IdempotencyService idempotency,
    BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.CreateOrder, ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Places an order after the customer has explicitly confirmed the items. Prices are calculated by the restaurant system, never by the assistant.")]
    public Task<string> ExecuteAsync(
        [Description("Items to order.")] OrderItemInput[] items,
        [Description("Order notes for the kitchen.")] string? notes = null,
        [Description("Branch id. Optional when the session is bound to a branch.")] Guid? branchId = null,
        [Description("Optional client-generated idempotency key for this confirmation.")] string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var caller = authorization.Authorize(McpToolNames.CreateOrder, branchId);
        var body = new
        {
            branchId = branchId ?? caller.Branch.BranchId!.Value,
            tableId = caller.Branch.TableId,
            channel = "AiWaiter",
            items = items.Select(i => new { i.MenuItemId, i.Quantity, i.Notes }).ToArray(),
            notes,
        };

        return audit.TrackAsync(McpToolNames.CreateOrder, caller, () =>
            idempotency.ExecuteAsync(
                McpToolNames.CreateOrder,
                caller,
                body,
                idempotencyKey,
                (key, ct) => api.PostAsync("/api/orders", body, key, ct),
                cancellationToken));
    }
}
