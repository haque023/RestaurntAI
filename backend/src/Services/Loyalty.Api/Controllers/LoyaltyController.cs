using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Loyalty.Api.Controllers;

[ApiController]
[Route("api/loyalty")]
[Tags("Loyalty")]
public sealed class LoyaltyController(ILoyaltyService service) : ControllerBase
{
    [HttpGet("balance")]
    [RequirePermission(PlatformPermissions.LoyaltyRead)]
    public Task<IResult> GetBalanceAsync(CancellationToken cancellationToken) =>
        service.GetBalanceAsync(cancellationToken);
}
