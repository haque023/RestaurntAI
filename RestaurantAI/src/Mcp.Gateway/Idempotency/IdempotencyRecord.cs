namespace RestaurantAI.Mcp.Gateway.Idempotency;

public enum IdempotencyStatus
{
    Completed,
    Failed,
}

/// <summary>
/// The cached outcome of a mutating tool call, so a model that repeats the same call in the
/// same turn gets the original result instead of creating a second order or reservation.
/// </summary>
public sealed record IdempotencyRecord(
    string Key,
    string ToolName,
    Guid TenantId,
    Guid UserId,
    string ArgumentsHash,
    IdempotencyStatus Status,
    string Result,
    DateTimeOffset CreatedAt);
