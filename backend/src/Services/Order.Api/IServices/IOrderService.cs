namespace RestaurantAI.Order.Api.IServices;

public interface IOrderService
{
    Task<IResult> PlaceOrderAsync(PlaceOrderCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken);

    Task<IResult> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IResult> CancelOrderAsync(Guid orderId, CancelOrderCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken);

    Task<IResult> ChangeStatusAsync(Guid orderId, ChangeOrderStatusCommand command, CancellationToken cancellationToken);

    Task<IResult> ListAsync(Guid? branchId, string? status, DateTimeOffset? since, int? take, CancellationToken cancellationToken);

    Task<IResult> ListMineAsync(CancellationToken cancellationToken);
}
