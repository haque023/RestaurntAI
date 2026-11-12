using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Menu.Api.Controllers;

[ApiController]
[Route("api/menu")]
[Tags("Menu")]
public sealed class MenuController(IMenuService service) : ControllerBase
{
    [HttpGet("branches/{branchId:guid}/items")]
    [RequirePermission(PlatformPermissions.MenuRead)]
    public Task<IResult> GetMenuAsync(Guid branchId, CancellationToken cancellationToken) =>
        service.GetMenuAsync(branchId, cancellationToken);

    [HttpGet("branches/{branchId:guid}/items/search")]
    [RequirePermission(PlatformPermissions.MenuRead)]
    public Task<IResult> SearchAsync(Guid branchId, string? q, decimal? maxPrice, string? dietary, int? limit, CancellationToken cancellationToken) =>
        service.SearchAsync(branchId, q, maxPrice, dietary, limit, cancellationToken);

    [HttpGet("branches/{branchId:guid}/items/batch")]
    [RequirePermission(PlatformPermissions.MenuRead)]
    public Task<IResult> GetBatchAsync(Guid branchId, [BindRequired] string ids, CancellationToken cancellationToken) =>
        service.GetBatchAsync(branchId, ids, cancellationToken);

    [HttpGet("items/{itemId:guid}/availability")]
    [RequirePermission(PlatformPermissions.MenuRead)]
    public Task<IResult> GetAvailabilityAsync(Guid itemId, CancellationToken cancellationToken) =>
        service.GetAvailabilityAsync(itemId, cancellationToken);

    [HttpPost("branches/{branchId:guid}/items")]
    [RequirePermission(PlatformPermissions.MenuWrite)]
    public Task<IResult> CreateAsync(Guid branchId, CreateMenuItemCommand command, CancellationToken cancellationToken) =>
        service.CreateAsync(branchId, command, cancellationToken);

    [HttpPut("items/{itemId:guid}/availability")]
    [RequirePermission(PlatformPermissions.MenuWrite)]
    public Task<IResult> SetAvailabilityAsync(Guid itemId, SetAvailabilityRequest request, CancellationToken cancellationToken) =>
        service.SetAvailabilityAsync(itemId, request, cancellationToken);

    [HttpPut("items/{itemId:guid}")]
    [RequirePermission(PlatformPermissions.MenuWrite)]
    public Task<IResult> UpdateAsync(Guid itemId, UpdateMenuItemCommand command, CancellationToken cancellationToken) =>
        service.UpdateAsync(itemId, command, cancellationToken);

    [HttpDelete("items/{itemId:guid}")]
    [RequirePermission(PlatformPermissions.MenuWrite)]
    public Task<IResult> DeleteAsync(Guid itemId, CancellationToken cancellationToken) =>
        service.DeleteAsync(itemId, cancellationToken);
}
