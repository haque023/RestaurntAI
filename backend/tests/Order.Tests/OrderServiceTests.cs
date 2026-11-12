using NSubstitute;
using RestaurantAI.Order.Api.IServices;
using RestaurantAI.Order.Api.Models;
using RestaurantAI.Order.Api.Services;
using RestaurantAI.SharedKernel.Results;

namespace RestaurantAI.Order.Tests;

public sealed class OrderServiceTests
{
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Ramen = Guid.NewGuid();

    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IMenuCatalog _catalog = Substitute.For<IMenuCatalog>();

    private OrderWorkflow CreateService(FakeTenantContext? tenant = null) =>
        new(_orders, _catalog, tenant ?? new FakeTenantContext(), TimeProvider.System);

    private void CatalogReturns(params CatalogItem[] items) =>
        _catalog.GetItemsAsync(Branch, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(items.ToDictionary(i => i.Id));

    [Fact]
    public async Task Uses_the_menu_service_price_not_client_input()
    {
        CatalogReturns(new CatalogItem(Ramen, "Spicy Ramen", 13.90m, "USD", IsAvailable: true));

        var result = await CreateService().PlaceAsync(
            new PlaceOrderCommand(Branch, null, OrderChannel.AiWaiter, [new PlaceOrderItem(Ramen, 2, null)], null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(27.80m, result.Value.Total);
        _orders.Received(1).Add(result.Value);
    }

    [Fact]
    public async Task Rejects_unavailable_items()
    {
        CatalogReturns(new CatalogItem(Ramen, "Spicy Ramen", 13.90m, "USD", IsAvailable: false));

        var result = await CreateService().PlaceAsync(
            new PlaceOrderCommand(Branch, null, OrderChannel.AiWaiter, [new PlaceOrderItem(Ramen, 1, null)], null),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _orders.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Rejects_items_that_do_not_belong_to_the_branch()
    {
        CatalogReturns();

        var result = await CreateService().PlaceAsync(
            new PlaceOrderCommand(Branch, null, OrderChannel.ConsumerApp, [new PlaceOrderItem(Ramen, 1, null)], null),
            TestContext.Current.CancellationToken);

        Assert.Equal("order.unknown_item", result.Error?.Code);
    }

    [Fact]
    public async Task Staff_bound_to_another_branch_cannot_place_orders()
    {
        var result = await CreateService(new FakeTenantContext { BranchId = Guid.NewGuid() }).PlaceAsync(
            new PlaceOrderCommand(Branch, null, OrderChannel.Staff, [new PlaceOrderItem(Ramen, 1, null)], null),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderWorkflow.BranchForbidden, result.Error);
    }
}
