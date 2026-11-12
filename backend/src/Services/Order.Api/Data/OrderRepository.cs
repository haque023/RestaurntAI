using Microsoft.EntityFrameworkCore;

namespace RestaurantAI.Order.Api.Data;

internal sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public void Add(Models.Order order) => dbContext.Orders.Add(order);

    public Task<Models.Order?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
}
