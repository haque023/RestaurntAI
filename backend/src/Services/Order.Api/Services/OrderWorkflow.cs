using RestaurantAI.SharedKernel.Domain;
using RestaurantAI.SharedKernel.Results;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Services;

/// <summary>
/// Order use cases. Changes are staged on the unit of work; the caller commits them together
/// with the idempotency record so retries can never create duplicate orders.
/// </summary>
public sealed class OrderWorkflow(
    IOrderRepository orders,
    IMenuCatalog menuCatalog,
    ITenantContext tenant,
    TimeProvider timeProvider)
{
    public static readonly Error TenantRequired = Error.Forbidden("tenant.required", "A tenant context is required.");
    public static readonly Error BranchForbidden = Error.Forbidden("branch.forbidden", "You do not have access to this branch.");
    public static readonly Error OrderNotFound = Error.NotFound("order.not_found", "Order not found.");

    public async Task<Result<Models.Order>> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId || tenant.UserId is not { } userId)
        {
            return TenantRequired;
        }

        if (!CanAccessBranch(command.BranchId))
        {
            return BranchForbidden;
        }

        if (command.Items.Count == 0)
        {
            return Error.Validation("order.empty", "An order must contain at least one item.");
        }

        var ids = command.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var catalog = await menuCatalog.GetItemsAsync(command.BranchId, ids, cancellationToken);

        var lines = new List<OrderLine>(command.Items.Count);
        foreach (var item in command.Items)
        {
            if (!catalog.TryGetValue(item.MenuItemId, out var menuItem))
            {
                return Error.Validation("order.unknown_item", $"Menu item {item.MenuItemId} does not exist in this branch.");
            }

            if (!menuItem.IsAvailable)
            {
                return Error.Conflict("order.item_unavailable", $"'{menuItem.Name}' is currently unavailable.");
            }

            lines.Add(OrderLine.Create(menuItem.Id, menuItem.Name, item.Quantity, new Money(menuItem.Price, menuItem.Currency), item.Notes));
        }

        // Guests are seated only by their QR session; staff may place orders for any table.
        var tableId = tenant.HasPermission(PlatformPermissions.OrdersManage) ? command.TableId ?? tenant.TableId : tenant.TableId;

        var order = Models.Order.Place(
            tenantId,
            command.BranchId,
            tableId,
            userId,
            command.Channel,
            lines,
            command.Notes,
            timeProvider.GetUtcNow());

        orders.Add(order);
        return order;
    }

    public async Task<Result<Models.Order>> GetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(orderId, cancellationToken);
        if (order is null || !CanAccessBranch(order.BranchId) || !CanSeeOrder(order))
        {
            return OrderNotFound;
        }

        return order;
    }

    public async Task<Result<Models.Order>> CancelAsync(Guid orderId, CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var found = await GetAsync(orderId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found;
        }

        var order = found.Value;
        if (!order.CanTransitionTo(OrderStatus.Cancelled))
        {
            return Error.Conflict("order.not_cancellable", $"An order in status {order.Status} cannot be cancelled.");
        }

        order.Cancel(command.Reason, timeProvider.GetUtcNow(), tenant.UserId);
        return order;
    }

    /// <summary>Staff moves an order along the kitchen flow, or rejects it before acceptance.</summary>
    public async Task<Result<Models.Order>> ChangeStatusAsync(Guid orderId, ChangeOrderStatusCommand command, CancellationToken cancellationToken)
    {
        if (!tenant.HasPermission(PlatformPermissions.OrdersManage))
        {
            return Error.Forbidden("permission.denied", "Only restaurant staff can change order status.");
        }

        var found = await GetAsync(orderId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found;
        }

        var order = found.Value;
        if (!order.CanTransitionTo(command.Status))
        {
            return Error.Conflict("order.invalid_transition", $"An order in status {order.Status} cannot move to {command.Status}.");
        }

        var now = timeProvider.GetUtcNow();
        switch (command.Status)
        {
            case OrderStatus.Rejected:
                order.Reject(command.Reason ?? string.Empty, now, tenant.UserId!.Value);
                break;
            case OrderStatus.Cancelled:
                order.Cancel(string.IsNullOrWhiteSpace(command.Reason) ? "Cancelled by the restaurant." : command.Reason.Trim(), now, tenant.UserId);
                break;
            default:
                order.Advance(command.Status, now, tenant.UserId);
                break;
        }

        return order;
    }

    private bool CanAccessBranch(Guid branchId) =>
        tenant.IsPlatformAdministrator || tenant.BranchId is null || tenant.BranchId == branchId;

    // Customers only see their own orders; staff with orders.manage see the branch's orders.
    private bool CanSeeOrder(Models.Order order) =>
        tenant.HasPermission(PlatformPermissions.OrdersManage) || order.CustomerId == tenant.UserId;
}
