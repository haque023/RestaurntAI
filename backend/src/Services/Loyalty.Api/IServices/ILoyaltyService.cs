namespace RestaurantAI.Loyalty.Api.IServices;

public interface ILoyaltyService
{
    Task<IResult> GetBalanceAsync(CancellationToken cancellationToken);
}
