namespace RestaurantAI.Infrastructure.Idempotency;

/// <summary>
/// The stored outcome of a mutation executed with an <c>Idempotency-Key</c>.
/// Persisted in the same transaction as the business change it guards.
/// </summary>
public sealed class IdempotencyEntry
{
    private IdempotencyEntry()
    {
    }

    public Guid TenantId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    /// <summary>Logical operation, e.g. <c>orders.create</c>. A key may not be reused across operations.</summary>
    public string Operation { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public int StatusCode { get; private set; }

    public string ResponseBody { get; private set; } = "null";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static IdempotencyEntry Create(
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        int statusCode,
        string responseBody,
        DateTimeOffset now,
        TimeSpan retention) => new()
    {
        TenantId = tenantId,
        Key = key,
        Operation = operation,
        RequestHash = requestHash,
        StatusCode = statusCode,
        ResponseBody = responseBody,
        CreatedAt = now,
        ExpiresAt = now.Add(retention),
    };
}
