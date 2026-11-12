namespace RestaurantAI.Loyalty.Api.IServices;

public interface ILoyaltyAccountRepository
{
    void Add(LoyaltyAccount account);

    Task<LoyaltyAccount?> FindByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken);
}
