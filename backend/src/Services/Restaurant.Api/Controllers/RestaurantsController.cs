using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Controllers;

[ApiController]
[Route("api/restaurants")]
[Tags("Restaurants")]
public sealed class RestaurantsController(IRestaurantService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = PlatformRoles.PlatformAdministrator)]
    public Task<IResult> RegisterAsync(RegisterRestaurantRequest request, CancellationToken cancellationToken) =>
        service.RegisterAsync(request, cancellationToken);

    [HttpGet("current")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> GetCurrentAsync(CancellationToken cancellationToken) =>
        service.GetCurrentAsync(cancellationToken);

    [HttpGet("current/summary")]
    [RequirePermission(PlatformPermissions.MenuRead)]
    public Task<IResult> GetSummaryAsync(CancellationToken cancellationToken) =>
        service.GetSummaryAsync(cancellationToken);

    [HttpPut("current/profile")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        service.UpdateProfileAsync(request, cancellationToken);

    [HttpPut("current/listing")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> SetListingAsync(SetListingRequest request, CancellationToken cancellationToken) =>
        service.SetListingAsync(request, cancellationToken);

    [HttpPost("current/branches")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> AddBranchAsync(AddBranchRequest request, CancellationToken cancellationToken) =>
        service.AddBranchAsync(request, cancellationToken);

    [HttpPut("current/branches/{branchId:guid}")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken) =>
        service.UpdateBranchAsync(branchId, request, cancellationToken);

    [HttpGet("current/ai-settings")]
    [RequirePermission(PlatformPermissions.AiManage)]
    public Task<IResult> GetAiSettingsAsync(CancellationToken cancellationToken) =>
        service.GetAiSettingsAsync(cancellationToken);

    [HttpPut("current/ai-settings")]
    [RequirePermission(PlatformPermissions.AiManage)]
    public Task<IResult> UpdateAiSettingsAsync(AiSettingsRequest request, CancellationToken cancellationToken) =>
        service.UpdateAiSettingsAsync(request, cancellationToken);

    [HttpGet("plans")]
    [Authorize]
    public IResult GetPlans() =>
        service.GetPlans();

    [HttpGet("current/ai-context")]
    [Authorize]
    public Task<IResult> GetAiContextAsync(CancellationToken cancellationToken) =>
        service.GetAiContextAsync(cancellationToken);
}
