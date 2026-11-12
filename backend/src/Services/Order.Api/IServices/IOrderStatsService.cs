namespace RestaurantAI.Order.Api.IServices;

public interface IOrderStatsService
{
    Task<IResult> GetStatsAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? branchId, CancellationToken cancellationToken);
}
