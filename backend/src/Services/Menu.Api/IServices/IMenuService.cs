namespace RestaurantAI.Menu.Api.IServices;

public interface IMenuService
{
    Task<IResult> GetMenuAsync(Guid branchId, CancellationToken cancellationToken);

    Task<IResult> SearchAsync(Guid branchId, string? q, decimal? maxPrice, string? dietary, int? limit, CancellationToken cancellationToken);

    Task<IResult> GetBatchAsync(Guid branchId, string ids, CancellationToken cancellationToken);

    Task<IResult> GetAvailabilityAsync(Guid itemId, CancellationToken cancellationToken);

    Task<IResult> CreateAsync(Guid branchId, CreateMenuItemCommand command, CancellationToken cancellationToken);

    Task<IResult> SetAvailabilityAsync(Guid itemId, SetAvailabilityRequest request, CancellationToken cancellationToken);

    Task<IResult> UpdateAsync(Guid itemId, UpdateMenuItemCommand command, CancellationToken cancellationToken);

    Task<IResult> DeleteAsync(Guid itemId, CancellationToken cancellationToken);
}
