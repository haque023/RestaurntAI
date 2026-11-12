using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Loyalty.Api.Services;

public sealed class LoyaltyService(ILoyaltyAccountRepository accounts, ITenantContext caller, TimeProvider timeProvider) : ILoyaltyService
{
    /// <summary>Points earned per whole currency unit spent.</summary>
    public const int PointsPerCurrencyUnit = 1;

    public async Task<IResult> GetBalanceAsync(CancellationToken cancellationToken)
    {
        if (caller.TenantId is not { } tenantId || caller.UserId is not { } userId)
        {
            return TypedResults.Forbid();
        }

        var account = await accounts.FindByCustomerAsync(tenantId, userId, cancellationToken);
        return TypedResults.Ok(account is null
            ? new LoyaltyBalanceResponse(Guid.Empty, userId, 0)
            : new LoyaltyBalanceResponse(account.Id, userId, account.Balance));
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
