using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Consumer.Api.Controllers;

/// <summary>"My restaurants": favorites are keyed by the signed-in user only, across all restaurants.</summary>
[ApiController]
[Route("api/consumer/favorites")]
[Tags("Consumer")]
public sealed class FavoritesController(IFavoriteService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PlatformPermissions.ConsumerProfile, RequireTenant = false)]
    public Task<IResult> ListAsync(CancellationToken cancellationToken) =>
        service.ListAsync(cancellationToken);

    [HttpPut("{restaurantId:guid}")]
    [RequirePermission(PlatformPermissions.ConsumerProfile, RequireTenant = false)]
    public Task<IResult> AddAsync(Guid restaurantId, CancellationToken cancellationToken) =>
        service.AddAsync(restaurantId, cancellationToken);

    [HttpDelete("{restaurantId:guid}")]
    [RequirePermission(PlatformPermissions.ConsumerProfile, RequireTenant = false)]
    public Task<IResult> RemoveAsync(Guid restaurantId, CancellationToken cancellationToken) =>
        service.RemoveAsync(restaurantId, cancellationToken);
}
