using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Controllers;

/// <summary>
/// Platform administration of the restaurant lifecycle: verification, suspension, plans and AI
/// governance. Runs under the separate platform-administrator policy and sees every tenant.
/// </summary>
[ApiController]
[Route("api/admin/restaurants")]
[Tags("Admin")]
[Authorize(Roles = PlatformRoles.PlatformAdministrator)]
public sealed class AdminRestaurantsController(IAdminRestaurantService service) : ControllerBase
{
    [HttpGet]
    public Task<IResult> ListAsync(string? status, string? search, CancellationToken cancellationToken) =>
        service.ListAsync(status, search, cancellationToken);

    [HttpGet("stats")]
    public Task<IResult> StatsAsync(CancellationToken cancellationToken) =>
        service.StatsAsync(cancellationToken);

    [HttpGet("{restaurantId:guid}")]
    public Task<IResult> GetAsync(Guid restaurantId, CancellationToken cancellationToken) =>
        service.GetAsync(restaurantId, cancellationToken);

    [HttpPost("{restaurantId:guid}/approve")]
    public Task<IResult> ApproveAsync(Guid restaurantId, CancellationToken cancellationToken) =>
        service.ApproveAsync(restaurantId, cancellationToken);

    [HttpPost("{restaurantId:guid}/suspend")]
    public Task<IResult> SuspendAsync(Guid restaurantId, SuspendRequest request, CancellationToken cancellationToken) =>
        service.SuspendAsync(restaurantId, request, cancellationToken);

    [HttpPost("{restaurantId:guid}/reinstate")]
    public Task<IResult> ReinstateAsync(Guid restaurantId, CancellationToken cancellationToken) =>
        service.ReinstateAsync(restaurantId, cancellationToken);

    [HttpPut("{restaurantId:guid}/plan")]
    public Task<IResult> ChangePlanAsync(Guid restaurantId, ChangePlanRequest request, CancellationToken cancellationToken) =>
        service.ChangePlanAsync(restaurantId, request, cancellationToken);

    [HttpPut("{restaurantId:guid}/ai")]
    public Task<IResult> SetAiAsync(Guid restaurantId, SetAiRequest request, CancellationToken cancellationToken) =>
        service.SetAiAsync(restaurantId, request, cancellationToken);
}
