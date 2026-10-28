using RestaurantAI.AI.Context;

namespace RestaurantAI.Context.Orchestrator.Tests;

public sealed class ContextOrchestrationTests
{
    private static readonly ContextRankingEngine Ranking = new(TimeProvider.System);

    private static ContextEvictionPolicy Policy(int maxInputTokens) =>
        new(new TokenBudgetManager(new TokenBudgetOptions { MaxInputTokens = maxInputTokens }));

    private static string Text(int tokens) => new('x', tokens * 4);

    [Fact]
    public void Live_business_data_supersedes_retrieved_knowledge_for_the_same_entity()
    {
        var items = new ContextBuilder()
            .AddSystem("system", "You are the AI waiter.")
            .AddLiveData("live:ramen", "Spicy Ramen: 13.90 USD, available", entityKey: "menu-item:ramen")
            .AddRetrieved("kb:ramen", "Spicy Ramen costs 11.00 USD", similarity: 0.95, entityKey: "menu-item:ramen")
            .Items;

        var result = Policy(10_000).Apply(Ranking.Rank(items));

        Assert.DoesNotContain(result.Included, i => i.Id == "kb:ramen");
        Assert.Contains(result.Evicted, e => e.Item.Id == "kb:ramen" && e.Reason == "superseded-by-live-data");
        Assert.Contains(result.Included, i => i.Id == "live:ramen");
    }

    [Fact]
    public void Never_exceeds_the_token_budget()
    {
        var builder = new ContextBuilder().AddSystem("system", Text(100));
        for (var i = 0; i < 50; i++)
        {
            builder.AddRetrieved($"kb:{i}", Text(200), similarity: 0.5 + (i / 100.0));
        }

        var result = Policy(2_000).Apply(Ranking.Rank(builder.Items));

        Assert.True(result.TokensUsed <= 2_000);
        Assert.NotEmpty(result.Evicted);
    }

    [Fact]
    public void Pinned_items_survive_even_when_the_budget_is_tight()
    {
        var items = new ContextBuilder()
            .AddSystem("system", Text(50))
            .AddHistory("turn:last", "User: " + Text(50), DateTimeOffset.UtcNow, pinned: true)
            .AddRetrieved("kb:1", Text(500), similarity: 0.99)
            .Items;

        var result = Policy(120).Apply(Ranking.Rank(items));

        Assert.Contains(result.Included, i => i.Id == "system");
        Assert.Contains(result.Included, i => i.Id == "turn:last");
        Assert.Contains(result.Evicted, e => e.Item.Id == "kb:1");
    }

    [Fact]
    public void Higher_relevance_retrieval_wins_when_only_one_fits()
    {
        var items = new ContextBuilder()
            .AddRetrieved("kb:low", Text(60), similarity: 0.3)
            .AddRetrieved("kb:high", Text(60), similarity: 0.9)
            .Items;

        var result = Policy(100).Apply(Ranking.Rank(items));

        Assert.Equal("kb:high", Assert.Single(result.Included).Id);
    }

    [Fact]
    public void Live_data_ranks_above_retrieval_at_equal_relevance()
    {
        var ranked = Ranking.Rank(new ContextBuilder()
            .AddRetrieved("kb", "policy text", similarity: 1)
            .AddLiveData("live", "order status: Preparing")
            .Items);

        Assert.Equal("live", ranked[0].Item.Id);
    }

    [Fact]
    public void Budget_respects_output_and_tool_reservations()
    {
        var budget = new TokenBudgetManager(new TokenBudgetOptions
        {
            ContextWindow = 16_000,
            ReservedForOutput = 2_000,
            ReservedForTools = 4_000,
            MaxInputTokens = 50_000,
        });

        Assert.Equal(10_000, budget.TotalInputBudget);
    }
}
