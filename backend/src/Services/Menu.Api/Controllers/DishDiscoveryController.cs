using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantAI.Menu.Api.Controllers;

/// <summary>Public dish search across restaurants ("where can I get ramen?").</summary>
[ApiController]
[Route("api/discovery/menu-items")]
[Tags("Discovery")]
[AllowAnonymous]
public sealed class DishDiscoveryController(IDishDiscoveryService service) : ControllerBase
{
    [HttpGet]
    public Task<IResult> SearchAsync(string? q, string? diets, decimal? maxPrice, string? branchIds, int? limit, CancellationToken cancellationToken) =>
        service.SearchAsync(q, diets, maxPrice, branchIds, limit, cancellationToken);
}
