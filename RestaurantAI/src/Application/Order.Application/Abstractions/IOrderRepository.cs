namespace RestaurantAI.Order.Application.Abstractions;

public interface IOrderRepository
{
    void Add(Domain.Order order);

    Task<Domain.Order?> GetAsync(Guid orderId, CancellationToken cancellationToken);
}
