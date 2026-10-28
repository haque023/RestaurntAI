using NSubstitute;
using RestaurantAI.Order.Application.Abstractions;
using RestaurantAI.Order.Application.Orders;
using RestaurantAI.Order.Domain;
using RestaurantAI.SharedKernel.Domain;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Tests;

public sealed class OrderLifecycleTests
{
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Fuchka = Guid.NewGuid();

    private static Domain.Order NewOrder() => Domain.Order.Place(
        Guid.NewGuid(), Branch, null, Guid.NewGuid(), OrderChannel.QrTable,
        [OrderLine.Create(Fuchka, "Fuchka", 2, new Money(180, "BDT"), null)], null, DateTimeOffset.UtcNow);

    [Fact]
    public void Kitchen_flow_records_every_status()
    {
        var order = NewOrder();
        var staff = Guid.NewGuid();
        while (order.NextStatus is { } next)
        {
            order.Advance(next, DateTimeOffset.UtcNow, staff);
        }

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(
            [OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served, OrderStatus.Completed],
            order.History.Select(h => h.Status));
    }

    [Fact]
    public void Only_new_orders_can_be_rejected()
    {
        var order = NewOrder();
        order.Reject("  ", DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("Rejected by the restaurant.", order.CancellationReason);

        var accepted = NewOrder();
        accepted.Advance(OrderStatus.Accepted);
        Assert.Throws<DomainException>(() => accepted.Reject("busy", DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Advance_cannot_be_used_to_stop_an_order() =>
        Assert.Throws<DomainException>(() => NewOrder().Advance(OrderStatus.Rejected));

    [Fact]
    public async Task Customers_cannot_change_order_status()
    {
        var orders = Substitute.For<IOrderRepository>();
        var order = NewOrder();
        orders.GetAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var service = new OrderService(orders, Substitute.For<IMenuCatalog>(), new FakeTenantContext { Permissions = new HashSet<string> { PlatformPermissions.OrdersRead } }, TimeProvider.System);

        var result = await service.ChangeStatusAsync(order.Id, new ChangeOrderStatusCommand(OrderStatus.Accepted, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Placed, order.Status);
    }

    [Fact]
    public async Task Guests_are_seated_only_by_their_qr_session()
    {
        var orders = Substitute.For<IOrderRepository>();
        var catalog = Substitute.For<IMenuCatalog>();
        catalog.GetItemsAsync(Branch, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CatalogItem> { [Fuchka] = new(Fuchka, "Fuchka", 180, "BDT", true) });
        var qrTable = Guid.NewGuid();
        var guest = new FakeTenantContext { TableId = qrTable, Permissions = new HashSet<string> { PlatformPermissions.OrdersCreate } };
        var service = new OrderService(orders, catalog, guest, TimeProvider.System);

        var result = await service.PlaceAsync(new PlaceOrderCommand(Branch, Guid.NewGuid(), OrderChannel.QrTable, [new PlaceOrderItem(Fuchka, 1, null)], null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(qrTable, result.Value.TableId);
    }

    [Fact]
    public void Service_requests_move_from_open_to_resolved()
    {
        var request = ServiceRequest.Open(Guid.NewGuid(), Branch, null, Guid.NewGuid(), ServiceRequestKind.AiEscalation, new string('x', 600), DateTimeOffset.UtcNow);
        Assert.Equal(ServiceRequest.MaxNoteLength, request.Note.Length);

        var staff = Guid.NewGuid();
        request.Acknowledge(staff);
        Assert.Equal(ServiceRequestStatus.Acknowledged, request.Status);

        request.Resolve(staff, DateTimeOffset.UtcNow);
        Assert.Equal(ServiceRequestStatus.Resolved, request.Status);
        Assert.NotNull(request.ResolvedAt);
    }
}
