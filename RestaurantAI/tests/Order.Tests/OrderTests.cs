using RestaurantAI.Order.Domain;
using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Order.Tests;

public sealed class OrderTests
{
    private static Domain.Order NewOrder() => Domain.Order.Place(
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        Guid.NewGuid(),
        OrderChannel.AiWaiter,
        [OrderLine.Create(Guid.NewGuid(), "Spicy Ramen", 2, new Money(12.50m, "USD"), null)],
        null,
        DateTimeOffset.UtcNow);

    [Fact]
    public void Place_calculates_total_and_raises_OrderCreated()
    {
        var order = NewOrder();

        Assert.Equal(25.00m, order.Total);
        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.IsType<OrderCreated>(Assert.Single(order.DomainEvents));
    }

    [Fact]
    public void Place_rejects_an_empty_order() =>
        Assert.Throws<DomainException>(() => Domain.Order.Place(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), OrderChannel.Staff, [], null, DateTimeOffset.UtcNow));

    [Fact]
    public void Lines_reject_invalid_quantities() =>
        Assert.Throws<DomainException>(() => OrderLine.Create(Guid.NewGuid(), "Tea", 0, new Money(2m, "USD"), null));

    [Fact]
    public void Cannot_cancel_once_preparation_started()
    {
        var order = NewOrder();
        order.Advance(OrderStatus.Accepted);
        order.Advance(OrderStatus.Preparing);

        Assert.False(order.CanTransitionTo(OrderStatus.Cancelled));
        Assert.Throws<DomainException>(() => order.Cancel("changed my mind"));
    }

    [Fact]
    public void Completing_an_order_raises_OrderCompleted()
    {
        var order = NewOrder();
        foreach (var status in new[] { OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served, OrderStatus.Completed })
        {
            order.Advance(status);
        }

        Assert.IsType<OrderCompleted>(order.DomainEvents.Last());
    }
}
