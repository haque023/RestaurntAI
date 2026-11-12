namespace RestaurantAI.Restaurant.Api.IServices;

public interface IAdminRestaurantService
{
    Task<IResult> ListAsync(string? status, string? search, CancellationToken cancellationToken);

    Task<IResult> StatsAsync(CancellationToken cancellationToken);

    Task<IResult> GetAsync(Guid restaurantId, CancellationToken cancellationToken);

    Task<IResult> ApproveAsync(Guid restaurantId, CancellationToken cancellationToken);

    Task<IResult> SuspendAsync(Guid restaurantId, SuspendRequest request, CancellationToken cancellationToken);

    Task<IResult> ReinstateAsync(Guid restaurantId, CancellationToken cancellationToken);

    Task<IResult> ChangePlanAsync(Guid restaurantId, ChangePlanRequest request, CancellationToken cancellationToken);

    Task<IResult> SetAiAsync(Guid restaurantId, SetAiRequest request, CancellationToken cancellationToken);
}
