using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Controllers;

/// <summary>
/// Restaurant staff accounts: owners and managers add people with a role and an optional branch.
/// Permissions always follow the role (BR-007); only owners can appoint managers.
/// </summary>
[ApiController]
[Route("api/auth/staff")]
[Tags("Staff")]
public sealed class StaffController(IStaffService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PlatformPermissions.StaffManage)]
    public Task<IResult> ListAsync(CancellationToken cancellationToken) =>
        service.ListAsync(cancellationToken);

    [HttpGet("roles")]
    [RequirePermission(PlatformPermissions.StaffManage)]
    public IResult GetRoles() =>
        service.GetRoles();

    [HttpPost]
    [RequirePermission(PlatformPermissions.StaffManage)]
    public Task<IResult> AddAsync(AddStaffRequest request, CancellationToken cancellationToken) =>
        service.AddAsync(request, cancellationToken);

    [HttpPut("{userId:guid}")]
    [RequirePermission(PlatformPermissions.StaffManage)]
    public Task<IResult> UpdateAsync(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(userId, request, cancellationToken);

    [HttpDelete("{userId:guid}")]
    [RequirePermission(PlatformPermissions.StaffManage)]
    public Task<IResult> RemoveAsync(Guid userId, CancellationToken cancellationToken) =>
        service.RemoveAsync(userId, cancellationToken);
}
