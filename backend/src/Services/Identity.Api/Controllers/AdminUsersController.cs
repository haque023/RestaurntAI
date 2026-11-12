using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Controllers;

/// <summary>Platform-administrator user management (separate privileged policy).</summary>
[ApiController]
[Route("api/admin/identity")]
[Tags("Admin")]
[Authorize(Roles = PlatformRoles.PlatformAdministrator)]
public sealed class AdminUsersController(IAdminUserService service) : ControllerBase
{
    [HttpGet("users")]
    public Task<IResult> ListUsersAsync(string? search, int? take, CancellationToken cancellationToken) =>
        service.ListUsersAsync(search, take, cancellationToken);

    [HttpGet("tenants/{tenantId:guid}/members")]
    public Task<IResult> ListTenantMembersAsync(Guid tenantId, CancellationToken cancellationToken) =>
        service.ListTenantMembersAsync(tenantId, cancellationToken);

    [HttpPost("owners")]
    public Task<IResult> CreateOwnerAsync(CreateOwnerRequest request, CancellationToken cancellationToken) =>
        service.CreateOwnerAsync(request, cancellationToken);

    [HttpPost("users/{userId:guid}/deactivate")]
    public Task<IResult> DeactivateAsync(Guid userId, CancellationToken cancellationToken) =>
        service.DeactivateAsync(userId, cancellationToken);

    [HttpPost("users/{userId:guid}/activate")]
    public Task<IResult> ActivateAsync(Guid userId, CancellationToken cancellationToken) =>
        service.ActivateAsync(userId, cancellationToken);
}
