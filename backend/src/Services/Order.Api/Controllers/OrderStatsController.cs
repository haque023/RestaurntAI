using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Controllers;

/// <summary>Restaurant sales and operations KPIs for a period (default: the last 7 days).</summary>
[ApiController]
[Route("api/orders/stats")]
[Tags("Orders")]
public sealed class OrderStatsController(IOrderStatsService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PlatformPermissions.AnalyticsRead)]
    public Task<IResult> GetStatsAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? branchId, CancellationToken cancellationToken) =>
        service.GetStatsAsync(from, to, branchId, cancellationToken);
}
