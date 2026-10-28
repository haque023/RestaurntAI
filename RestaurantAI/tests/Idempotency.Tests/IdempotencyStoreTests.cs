using Microsoft.EntityFrameworkCore;
using RestaurantAI.Infrastructure.Idempotency;

namespace RestaurantAI.Idempotency.Tests;

public sealed class IdempotencyStoreTests
{
    private sealed class AnyContext : DbContext;

    [Fact]
    public void Request_hash_is_stable_for_equal_payloads() =>
        Assert.Equal(
            IdempotencyStore<AnyContext>.ComputeRequestHash(new { branchId = 1, items = new[] { "a", "b" } }),
            IdempotencyStore<AnyContext>.ComputeRequestHash(new { branchId = 1, items = new[] { "a", "b" } }));

    [Fact]
    public void Request_hash_changes_when_payload_changes() =>
        Assert.NotEqual(
            IdempotencyStore<AnyContext>.ComputeRequestHash(new { quantity = 1 }),
            IdempotencyStore<AnyContext>.ComputeRequestHash(new { quantity = 2 }));

    [Theory]
    [InlineData("mcp:0123abcd", true)]
    [InlineData("8d0c9b1e-2f4a-4c6d-9e8f-0a1b2c3d4e5f", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("contains space", false)]
    public void Validates_key_format(string? key, bool expected) =>
        Assert.Equal(expected, IdempotencyStore<AnyContext>.IsValidKey(key));

    [Fact]
    public void Rejects_keys_longer_than_128_characters() =>
        Assert.False(IdempotencyStore<AnyContext>.IsValidKey(new string('k', 129)));

    [Fact]
    public void Entry_expires_after_retention()
    {
        var now = new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

        var entry = IdempotencyEntry.Create(Guid.NewGuid(), "k1", "orders.place", "hash", 201, "{}", now, TimeSpan.FromHours(24));

        Assert.Equal(now.AddHours(24), entry.ExpiresAt);
    }
}
