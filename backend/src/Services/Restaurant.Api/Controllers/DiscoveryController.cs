using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantAI.Restaurant.Api.Controllers;

/// <summary>
/// Public, anonymous restaurant discovery for consumers. Reads only the discovery read model,
/// which contains listed (Active) restaurants and public branch data.
/// </summary>
[ApiController]
[Route("api/discovery/restaurants")]
[Tags("Discovery")]
[AllowAnonymous]
public sealed class DiscoveryController(IRestaurantDiscoveryService service) : ControllerBase
{
    [HttpGet]
    public Task<IResult> SearchAsync(string? q, string? cuisines, string? diets, double? lat, double? lng, double? radiusKm, bool? openNow, string? branchIds, string? tenantIds, int? limit, CancellationToken cancellationToken) =>
        service.SearchAsync(q, cuisines, diets, lat, lng, radiusKm, openNow, branchIds, tenantIds, limit, cancellationToken);

    [HttpGet("cuisines")]
    public Task<IResult> CuisinesAsync(CancellationToken cancellationToken) =>
        service.CuisinesAsync(cancellationToken);

    [HttpGet("tenants/{tenantId:guid}")]
    public Task<IResult> GetListedRestaurantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        service.GetListedRestaurantAsync(tenantId, cancellationToken);

    [HttpGet("qr/{token}")]
    public Task<IResult> ResolveQrAsync(string token, CancellationToken cancellationToken) =>
        service.ResolveQrAsync(token, cancellationToken);
}
