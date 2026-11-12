using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.AI.Api.Controllers;

[ApiController]
[Route("api/ai/waiter/turns")]
[Tags("AI")]
public sealed class WaiterController(IWaiterService service) : ControllerBase
{
    [HttpPost]
    [RequirePermission(PlatformPermissions.MenuRead)]
    public Task<IResult> TakeTurnAsync(WaiterTurnRequest request, CancellationToken cancellationToken) =>
        service.TakeTurnAsync(request, HttpContext, cancellationToken);
}
