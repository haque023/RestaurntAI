namespace RestaurantAI.Restaurant.Api.IServices;

public interface IRestaurantDiscoveryService
{
    Task<IResult> SearchAsync(string? q, string? cuisines, string? diets, double? lat, double? lng, double? radiusKm, bool? openNow, string? branchIds, string? tenantIds, int? limit, CancellationToken cancellationToken);

    Task<IResult> CuisinesAsync(CancellationToken cancellationToken);

    Task<IResult> GetListedRestaurantAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IResult> ResolveQrAsync(string token, CancellationToken cancellationToken);
}
