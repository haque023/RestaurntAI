
namespace RestaurantAI.Order.Api.Models;

public sealed record PlaceOrderCommand(
    Guid BranchId,
    Guid? TableId,
    OrderChannel Channel,
    IReadOnlyList<PlaceOrderItem> Items,
    string? Notes);

public sealed record PlaceOrderItem(Guid MenuItemId, int Quantity, string? Notes);

public sealed record CancelOrderCommand(string Reason);

public sealed record ChangeOrderStatusCommand(OrderStatus Status, string? Reason);

public sealed record OrderStatusChangeResponse(string Status, DateTimeOffset At);

public sealed record OrderLineResponse(Guid MenuItemId, string Name, int Quantity, decimal UnitPrice, decimal LineTotal, string? Notes);

public sealed record OrderResponse(
    Guid Id,
    Guid BranchId,
    Guid? TableId,
    string Status,
    decimal Total,
    string Currency,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderLineResponse> Lines,
    string Channel,
    string? Notes,
    string? CancellationReason,
    string? NextStatus,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderStatusChangeResponse> History)
{
    public static OrderResponse From(Models.Order order) => new(
        order.Id,
        order.BranchId,
        order.TableId,
        order.Status.ToString(),
        order.Total,
        order.Currency,
        order.PlacedAt,
        order.Lines.Select(l => new OrderLineResponse(l.MenuItemId, l.Name, l.Quantity, l.UnitPrice, l.LineTotal, l.Notes)).ToList(),
        order.Channel.ToString(),
        order.Notes,
        order.CancellationReason,
        order.NextStatus?.ToString(),
        order.UpdatedAt,
        order.History.Select(h => new OrderStatusChangeResponse(h.Status.ToString(), h.At)).ToList());
}
