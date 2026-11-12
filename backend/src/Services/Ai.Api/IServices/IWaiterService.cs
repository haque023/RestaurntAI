namespace RestaurantAI.AI.Api.IServices;

public interface IWaiterService
{
    Task<IResult> TakeTurnAsync(WaiterTurnRequest request, HttpContext httpContext, CancellationToken cancellationToken);
}
