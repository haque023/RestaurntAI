using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Consumer.Api.Controllers;

[ApiController]
[Route("api/consumer")]
[Tags("Consumer")]
public sealed class ConsumerController(IConsumerService service) : ControllerBase
{
    [HttpGet("profile")]
    [RequirePermission(PlatformPermissions.ConsumerProfile, RequireTenant = false)]
    public Task<IResult> GetProfileAsync(CancellationToken cancellationToken) =>
        service.GetProfileAsync(cancellationToken);

    [HttpPut("profile")]
    [RequirePermission(PlatformPermissions.ConsumerProfile, RequireTenant = false)]
    public Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        service.UpdateProfileAsync(request, cancellationToken);
}
