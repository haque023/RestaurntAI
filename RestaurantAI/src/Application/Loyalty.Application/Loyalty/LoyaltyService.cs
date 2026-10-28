using RestaurantAI.Loyalty.Domain;
using RestaurantAI.SharedKernel.Results;

namespace RestaurantAI.Loyalty.Application.Loyalty;

public sealed record LoyaltyBalanceResponse(Guid AccountId, Guid CustomerId, int Balance);

public interface ILoyaltyAccountRepository
{
    void Add(LoyaltyAccount account);

    Task<LoyaltyAccount?> FindByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken);
}

public sealed class LoyaltyService(ILoyaltyAccountRepository accounts, TimeProvider timeProvider)
{
    /// <summary>Points earned per whole currency unit spent.</summary>
    public const int PointsPerCurrencyUnit = 1;

    public async Task<Result<LoyaltyBalanceResponse>> GetBalanceAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByCustomerAsync(tenantId, customerId, cancellationToken);
        return account is null
            ? new LoyaltyBalanceResponse(Guid.Empty, customerId, 0)
            : new LoyaltyBalanceResponse(account.Id, customerId, account.Balance);
    }

    /// <summary>Handles OrderCompleted. Safe to call more than once for the same order.</summary>
    public async Task AwardForOrderAsync(Guid tenantId, Guid customerId, Guid orderId, decimal total, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByCustomerAsync(tenantId, customerId, cancellationToken);
        if (account is null)
        {
            account = LoyaltyAccount.Open(tenantId, customerId);
            accounts.Add(account);
        }

        account.Earn((int)Math.Floor(total) * PointsPerCurrencyUnit, "order", orderId, timeProvider.GetUtcNow());
    }
}
