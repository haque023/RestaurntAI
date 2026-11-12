namespace RestaurantAI.SharedKernel.Domain;

public sealed record Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount + other.Amount };
    }

    public Money Multiply(int quantity) => this with { Amount = Amount * quantity };

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new DomainException("money.currency_mismatch", $"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
