using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RestaurantAI.Infrastructure.Idempotency;

public enum IdempotencyOutcome
{
    /// <summary>The key has not been used; execute the operation.</summary>
    New,

    /// <summary>The same request was already executed; replay the stored response.</summary>
    Replay,

    /// <summary>The key was used with a different payload or operation; reject with 422.</summary>
    Mismatch,
}

public sealed record IdempotencyLookup(IdempotencyOutcome Outcome, int StatusCode = 0, string? ResponseBody = null);

/// <summary>
/// Guards mutations (orders, reservations, offline sync, MCP tool calls) against duplicates.
/// Usage: <see cref="LookupAsync"/> → execute → <see cref="Record{TResponse}"/> → SaveChanges
/// on the same DbContext, so the business change and the idempotency entry commit atomically.
/// A concurrent duplicate fails on the primary key; callers treat that as a replay.
/// </summary>
public sealed class IdempotencyStore<TContext>(IdempotencyRepository<TContext> repository, TimeProvider timeProvider)
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public TimeSpan Retention { get; init; } = TimeSpan.FromHours(24);

    public async Task<IdempotencyLookup> LookupAsync(
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(tenantId, key, cancellationToken);
        if (existing is null || existing.ExpiresAt < timeProvider.GetUtcNow())
        {
            return new IdempotencyLookup(IdempotencyOutcome.New);
        }

        return existing.Operation == operation && existing.RequestHash == requestHash
            ? new IdempotencyLookup(IdempotencyOutcome.Replay, existing.StatusCode, existing.ResponseBody)
            : new IdempotencyLookup(IdempotencyOutcome.Mismatch);
    }

    public void Record<TResponse>(Guid tenantId, string key, string operation, string requestHash, int statusCode, TResponse response) =>
        repository.Add(IdempotencyEntry.Create(
            tenantId,
            key,
            operation,
            requestHash,
            statusCode,
            JsonSerializer.Serialize(response, SerializerOptions),
            timeProvider.GetUtcNow(),
            Retention));

    public static string ComputeRequestHash<TRequest>(TRequest request)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions);
        return Convert.ToHexStringLower(SHA256.HashData(json));
    }

    public static bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= 128 && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.');

    public static T? Deserialize<T>(string? body) =>
        body is null ? default : JsonSerializer.Deserialize<T>(body, SerializerOptions);
}

public static class IdempotencyServiceCollectionExtensions
{
    public static IServiceCollection AddIdempotency<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IdempotencyRepository<TContext>>();
        services.AddScoped<IdempotencyStore<TContext>>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
