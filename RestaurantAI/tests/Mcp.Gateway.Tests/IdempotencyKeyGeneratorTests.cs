using RestaurantAI.Mcp.Gateway.Context;
using RestaurantAI.Mcp.Gateway.Idempotency;

namespace RestaurantAI.Mcp.Gateway.Tests;

public sealed class IdempotencyKeyGeneratorTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly IdempotencyKeyGenerator _generator = new();

    private static ToolCallerContext Caller(string? turn) => new(
        new UserContext(User, new HashSet<string>(), new HashSet<string>(), false),
        new RestaurantContext(Tenant),
        new BranchContext(null, null),
        ConversationId: "conv-1",
        TurnId: turn);

    [Fact]
    public void Same_call_in_same_turn_produces_same_key()
    {
        var args = new { items = new[] { new { id = 1, qty = 2 } } };

        Assert.Equal(
            _generator.Generate("create_order", Caller("turn-1"), args),
            _generator.Generate("create_order", Caller("turn-1"), args));
    }

    [Fact]
    public void Same_call_in_a_new_turn_produces_a_new_key()
    {
        var args = new { items = new[] { new { id = 1, qty = 2 } } };

        Assert.NotEqual(
            _generator.Generate("create_order", Caller("turn-1"), args),
            _generator.Generate("create_order", Caller("turn-2"), args));
    }

    [Fact]
    public void Different_arguments_produce_different_keys()
    {
        Assert.NotEqual(
            _generator.Generate("create_order", Caller("turn-1"), new { qty = 1 }),
            _generator.Generate("create_order", Caller("turn-1"), new { qty = 2 }));
    }

    [Fact]
    public void Keys_fit_the_business_api_key_format()
    {
        var key = _generator.Generate("create_order", Caller("turn-1"), new { qty = 1 });

        Assert.StartsWith("mcp:", key);
        Assert.True(key.Length <= 128);
    }
}
