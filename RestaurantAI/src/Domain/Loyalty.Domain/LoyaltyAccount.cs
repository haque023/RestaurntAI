using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Loyalty.Domain;

/// <summary>A customer's points balance with one restaurant (tenant).</summary>
public sealed class LoyaltyAccount : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<LoyaltyTransaction> _transactions = [];

    private LoyaltyAccount()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public int Balance { get; private set; }

    public IReadOnlyCollection<LoyaltyTransaction> Transactions => _transactions.AsReadOnly();

    public static LoyaltyAccount Open(Guid tenantId, Guid customerId) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenantId,
        CustomerId = customerId,
    };

    /// <summary>Earns points for a source (e.g. an order). Re-applying the same source is a no-op.</summary>
    public void Earn(int points, string sourceType, Guid sourceId, DateTimeOffset at)
    {
        if (points <= 0 || HasApplied(sourceType, sourceId))
        {
            return;
        }

        Apply(new LoyaltyTransaction(Guid.CreateVersion7(), points, sourceType, sourceId, at));
    }

    public void Redeem(int points, string sourceType, Guid sourceId, DateTimeOffset at)
    {
        if (points <= 0)
        {
            throw new DomainException("loyalty.invalid_points", "Points to redeem must be positive.");
        }

        if (HasApplied(sourceType, sourceId))
        {
            return;
        }

        if (points > Balance)
        {
            throw new DomainException("loyalty.insufficient_balance", "Insufficient loyalty balance.");
        }

        Apply(new LoyaltyTransaction(Guid.CreateVersion7(), -points, sourceType, sourceId, at));
    }

    private bool HasApplied(string sourceType, Guid sourceId) =>
        _transactions.Any(t => t.SourceType == sourceType && t.SourceId == sourceId);

    private void Apply(LoyaltyTransaction transaction)
    {
        _transactions.Add(transaction);
        Balance += transaction.Points;
        Raise(new LoyaltyUpdated(Id, TenantId, CustomerId, Balance));
    }
}

public sealed record LoyaltyTransaction(Guid Id, int Points, string SourceType, Guid SourceId, DateTimeOffset OccurredAt);

public sealed record LoyaltyUpdated(Guid AccountId, Guid TenantId, Guid CustomerId, int Balance) : DomainEvent;
