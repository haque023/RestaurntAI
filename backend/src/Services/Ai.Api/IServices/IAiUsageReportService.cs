namespace RestaurantAI.AI.Api.IServices;

public interface IAiUsageReportService
{
    Task<IResult> GetCurrentAsync(HttpContext httpContext, CancellationToken cancellationToken);

    Task<IResult> GetPlatformUsageAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);

    IResult GetAgents();
}
