using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using RestaurantAI.Mcp.Gateway.Context;

namespace RestaurantAI.Mcp.Gateway.Idempotency;

/// <summary>
/// First idempotency layer (Redis, short TTL): dedupes repeated tool calls from the model.
/// The generated key is also sent as <c>Idempotency-Key</c> to the business API, which
/// enforces durable idempotency in PostgreSQL (second layer).
/// </summary>
public sealed class IdempotencyService(
    IDistributedCache cache,
    IdempotencyKeyGenerator keyGenerator,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(1);

    public async Task<string> ExecuteAsync(
        string toolName,
        ToolCallerContext caller,
        object arguments,
        string? explicitKey,
        Func<string, CancellationToken, Task<string>> execute,
        CancellationToken cancellationToken)
    {
        var key = keyGenerator.Generate(toolName, caller, arguments, explicitKey);
        var argumentsHash = IdempotencyKeyGenerator.HashArguments(arguments);

        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null
            && JsonSerializer.Deserialize<IdempotencyRecord>(cached) is { Status: IdempotencyStatus.Completed } record
            && record.ArgumentsHash == argumentsHash)
        {
            return record.Result;
        }

        var result = await execute(key, cancellationToken);

        var entry = new IdempotencyRecord(
            key,
            toolName,
            caller.Restaurant.TenantId,
            caller.User.UserId,
            argumentsHash,
            IdempotencyStatus.Completed,
            result,
            timeProvider.GetUtcNow());

        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(entry),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Retention },
            cancellationToken);

        return result;
    }
}
