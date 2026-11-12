namespace RestaurantAI.AI.Api.IServices;

public interface IDiscoveryAssistantService
{
    Task<IResult> TakeTurnAsync(DiscoveryTurnRequest request, HttpContext httpContext, CancellationToken cancellationToken);
}
