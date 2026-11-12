namespace RestaurantAI.Order.Api.IServices;

public interface IOrderRepository
{
    void Add(Models.Order order);

    Task<Models.Order?> GetAsync(Guid orderId, CancellationToken cancellationToken);
}
