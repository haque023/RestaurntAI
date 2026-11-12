using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Admin.Api.Controllers;

/// <summary>
/// Platform administration: the platform overview and the cross-tenant audit log. Restaurant
/// lifecycle, users and AI governance live with the services that own that data (also under
/// /api/admin). Every endpoint runs under the separate privileged platform-administrator policy.
/// </summary>
[ApiController]
[Route("api/admin")]
[Tags("Admin")]
[Authorize(Roles = PlatformRoles.PlatformAdministrator)]
public sealed class AdminController(IAdminService service) : ControllerBase
{
    [HttpGet("overview")]
    public Task<IResult> OverviewAsync(CancellationToken cancellationToken) =>
        service.OverviewAsync(cancellationToken);

    [HttpGet("audit")]
    public Task<IResult> AuditAsync(string? type, string? module, Guid? tenantId, Guid? actorId, DateTimeOffset? from, DateTimeOffset? to, int? take, CancellationToken cancellationToken) =>
        service.AuditAsync(type, module, tenantId, actorId, from, to, take, cancellationToken);
}
