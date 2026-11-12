using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.AI.Api.Controllers;

/// <summary>AI telemetry: a restaurant's own usage against its plan, and the platform-wide view for administrators.</summary>
[ApiController]
[Route("api")]
[Tags("AI")]
public sealed class AiUsageController(IAiUsageReportService service) : ControllerBase
{
    [HttpGet("ai/usage/current")]
    [RequirePermission(PlatformPermissions.AnalyticsRead)]
    public Task<IResult> GetCurrentAsync(CancellationToken cancellationToken) =>
        service.GetCurrentAsync(HttpContext, cancellationToken);

    [HttpGet("admin/ai/usage")]
    [Authorize(Roles = PlatformRoles.PlatformAdministrator)]
    public Task<IResult> GetPlatformUsageAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
        service.GetPlatformUsageAsync(from, to, cancellationToken);

    [HttpGet("admin/ai/agents")]
    [Authorize(Roles = PlatformRoles.PlatformAdministrator)]
    public IResult GetAgents() =>
        service.GetAgents();
}
