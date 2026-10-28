using Microsoft.EntityFrameworkCore;
using RestaurantAI.Order.Application.Abstractions;

namespace RestaurantAI.Order.Api.Persistence;

internal sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public void Add(Domain.Order order) => dbContext.Orders.Add(order);

    public Task<Domain.Order?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
}
