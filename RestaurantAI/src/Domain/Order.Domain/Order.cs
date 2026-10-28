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

    /// <summary>Declined by the restaurant before it was accepted.</summary>
    Rejected,
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
        [OrderStatus.Placed] = [OrderStatus.Accepted, OrderStatus.Cancelled, OrderStatus.Rejected],
        [OrderStatus.Accepted] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Ready],
        [OrderStatus.Ready] = [OrderStatus.Served],
        [OrderStatus.Served] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Rejected] = [],
    };

    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusChange> _history = [];

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

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    /// <summary>Every status the order went through, for kitchen timing and auditing.</summary>
    public IReadOnlyCollection<OrderStatusChange> History => _history.AsReadOnly();

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
            UpdatedAt = now,
        };
        order._history.Add(new OrderStatusChange(OrderStatus.Placed, now, customerId));
        order._lines.AddRange(lines);
        order.Total = lines.Sum(l => l.LineTotal);
        order.Raise(new OrderCreated(order.Id, tenantId, branchId, customerId, order.Total, order.Currency));

        return order;
    }

    public void Cancel(string reason, DateTimeOffset? now = null, Guid? by = null)
    {
        TransitionTo(OrderStatus.Cancelled, now, by);
        CancellationReason = reason;
        Raise(new OrderCancelled(Id, TenantId, BranchId, reason));
    }

    public void Reject(string reason, DateTimeOffset now, Guid by)
    {
        TransitionTo(OrderStatus.Rejected, now, by);
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Rejected by the restaurant." : reason.Trim();
        Raise(new OrderRejected(Id, TenantId, BranchId, CancellationReason));
    }

    public void Advance(OrderStatus next, DateTimeOffset? now = null, Guid? by = null)
    {
        if (next is OrderStatus.Cancelled or OrderStatus.Rejected)
        {
            throw new DomainException("order.use_cancel", "Use Cancel or Reject to stop an order.");
        }

        TransitionTo(next, now, by);
        Raise(next == OrderStatus.Completed
            ? new OrderCompleted(Id, TenantId, BranchId, CustomerId, Total, Currency)
            : new OrderUpdated(Id, TenantId, BranchId, next));
    }

    public bool CanTransitionTo(OrderStatus next) => AllowedTransitions[Status].Contains(next);

    /// <summary>The next forward status in the normal flow, if any.</summary>
    public OrderStatus? NextStatus => Status switch
    {
        OrderStatus.Placed => OrderStatus.Accepted,
        OrderStatus.Accepted => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.Ready,
        OrderStatus.Ready => OrderStatus.Served,
        OrderStatus.Served => OrderStatus.Completed,
        _ => null,
    };

    private void TransitionTo(OrderStatus next, DateTimeOffset? now, Guid? by)
    {
        if (!CanTransitionTo(next))
        {
            throw new DomainException("order.invalid_transition", $"An order cannot move from {Status} to {next}.");
        }

        Status = next;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        _history.Add(new OrderStatusChange(next, UpdatedAt, by));
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

public sealed record OrderStatusChange(OrderStatus Status, DateTimeOffset At, Guid? By);

public sealed record OrderRejected(Guid OrderId, Guid TenantId, Guid BranchId, string Reason) : DomainEvent;

public sealed record OrderCreated(Guid OrderId, Guid TenantId, Guid BranchId, Guid CustomerId, decimal Total, string Currency) : DomainEvent;

public sealed record OrderUpdated(Guid OrderId, Guid TenantId, Guid BranchId, OrderStatus Status) : DomainEvent;

public sealed record OrderCompleted(Guid OrderId, Guid TenantId, Guid BranchId, Guid CustomerId, decimal Total, string Currency) : DomainEvent;

public sealed record OrderCancelled(Guid OrderId, Guid TenantId, Guid BranchId, string Reason) : DomainEvent;
