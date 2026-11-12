namespace RestaurantAI.Menu.Api.IServices;

public interface IDishDiscoveryService
{
    Task<IResult> SearchAsync(string? q, string? diets, decimal? maxPrice, string? branchIds, int? limit, CancellationToken cancellationToken);
}
