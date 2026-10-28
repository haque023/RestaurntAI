using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Order.Domain;

public enum OrderStatus
{
    Placed,
    Accepted,
    Preparing,
    Ready,
    Served,
    Completed,
    Cancelled,
}

public enum OrderChannel
{
    QrTable,
    ConsumerApp,
    ConsumerWeb,
    Staff,
    AiWaiter,
}

public sealed class Order : AggregateRoot<Guid>, IBranchScoped
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Placed] = [OrderStatus.Accepted, OrderStatus.Cancelled],
        [OrderStatus.Accepted] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Ready],
        [OrderStatus.Ready] = [OrderStatus.Served],
        [OrderStatus.Served] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
    };

    private readonly List<OrderLine> _lines = [];

    private Order()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid? TableId { get; private set; }

    public Guid CustomerId { get; private set; }

    public OrderChannel Channel { get; private set; }

    public OrderStatus Status { get; private set; }

    public string Currency { get; private set; } = "USD";

    public decimal Total { get; private set; }

    public string? Notes { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public static Order Place(
        Guid tenantId,
        Guid branchId,
        Guid? tableId,
        Guid customerId,
        OrderChannel channel,
        IReadOnlyCollection<OrderLine> lines,
        string? notes,
        DateTimeOffset now)
    {
        if (lines.Count == 0)
        {
            throw new DomainException("order.empty", "An order must contain at least one item.");
        }

        var currencies = lines.Select(l => l.Currency).Distinct().ToList();
        if (currencies.Count != 1)
        {
            throw new DomainException("order.mixed_currency", "All order lines must use the same currency.");
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BranchId = branchId,
            TableId = tableId,
            CustomerId = customerId,
            Channel = channel,
            Status = OrderStatus.Placed,
            Currency = currencies[0],
            Notes = notes,
            PlacedAt = now,
        };
        order._lines.AddRange(lines);
        order.Total = lines.Sum(l => l.LineTotal);
        order.Raise(new OrderCreated(order.Id, tenantId, branchId, customerId, order.Total, order.Currency));

        return order;
    }

    public void Cancel(string reason)
    {
        TransitionTo(OrderStatus.Cancelled);
        CancellationReason = reason;
        Raise(new OrderCancelled(Id, TenantId, BranchId, reason));
    }

    public void Advance(OrderStatus next)
    {
        if (next == OrderStatus.Cancelled)
        {
            throw new DomainException("order.use_cancel", "Use Cancel to cancel an order.");
        }

        TransitionTo(next);
        Raise(next == OrderStatus.Completed
            ? new OrderCompleted(Id, TenantId, BranchId, CustomerId, Total, Currency)
            : new OrderUpdated(Id, TenantId, BranchId, next));
    }

    public bool CanTransitionTo(OrderStatus next) => AllowedTransitions[Status].Contains(next);

    private void TransitionTo(OrderStatus next)
    {
        if (!CanTransitionTo(next))
        {
            throw new DomainException("order.invalid_transition", $"An order cannot move from {Status} to {next}.");
        }

        Status = next;
    }
}

/// <summary>An order line with the price captured from the menu service at order time.</summary>
public sealed record OrderLine(Guid MenuItemId, string Name, int Quantity, decimal UnitPrice, string Currency, string? Notes)
{
    public decimal LineTotal => UnitPrice * Quantity;

    public static OrderLine Create(Guid menuItemId, string name, int quantity, Money unitPrice, string? notes) =>
        quantity is < 1 or > 99
            ? throw new DomainException("order.invalid_quantity", "Quantity must be between 1 and 99.")
            : new OrderLine(menuItemId, name, quantity, unitPrice.Amount, unitPrice.Currency, notes);
}

public sealed record OrderCreated(Guid OrderId, Guid TenantId, Guid BranchId, Guid CustomerId, decimal Total, string Currency) : DomainEvent;

public sealed record OrderUpdated(Guid OrderId, Guid TenantId, Guid BranchId, OrderStatus Status) : DomainEvent;

public sealed record OrderCompleted(Guid OrderId, Guid TenantId, Guid BranchId, Guid CustomerId, decimal Total, string Currency) : DomainEvent;

public sealed record OrderCancelled(Guid OrderId, Guid TenantId, Guid BranchId, string Reason) : DomainEvent;
