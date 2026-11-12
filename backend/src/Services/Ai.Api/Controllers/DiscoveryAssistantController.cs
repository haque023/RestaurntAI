using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.AI.Api.Controllers;

/// <summary>
/// The platform discovery assistant: natural-language search for restaurants and dishes across
/// every listed restaurant, optionally near the customer. It has no restaurant (tenant) of its
/// own; history is kept per user in the distributed cache.
/// </summary>
[ApiController]
[Route("api/ai/discovery/turns")]
[Tags("AI")]
public sealed class DiscoveryAssistantController(IDiscoveryAssistantService service) : ControllerBase
{
    [HttpPost]
    [RequirePermission(PlatformPermissions.DiscoverySearch, RequireTenant = false)]
    public Task<IResult> TakeTurnAsync(DiscoveryTurnRequest request, CancellationToken cancellationToken) =>
        service.TakeTurnAsync(request, HttpContext, cancellationToken);
}
