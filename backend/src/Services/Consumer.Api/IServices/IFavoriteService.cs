namespace RestaurantAI.Consumer.Api.IServices;

public interface IFavoriteService
{
    Task<IResult> ListAsync(CancellationToken cancellationToken);

    Task<IResult> AddAsync(Guid restaurantId, CancellationToken cancellationToken);

    Task<IResult> RemoveAsync(Guid restaurantId, CancellationToken cancellationToken);
}
