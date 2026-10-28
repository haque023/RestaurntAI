using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.AI.Memory;
using RestaurantAI.AI.Rag;

namespace RestaurantAI.AI.Context;

public sealed record ContextRequest(
    Guid TenantId,
    Guid? BranchId,
    Guid UserId,
    Guid ConversationId,
    string UserMessage,
    string SystemInstructions,
    IReadOnlyList<ContextItem> LiveData);

public sealed record AssembledContext(IReadOnlyList<ChatMessage> Messages, IReadOnlyList<ContextItem> Included, IReadOnlyList<EvictedContextItem> Evicted, int TokensUsed, int TokenBudget);

/// <summary>
/// Context Orchestrator: gathers memory, conversation history, retrieved knowledge and live
/// business data, ranks it, fits it into the token budget and produces chat messages for Foundry.
/// </summary>
public sealed class ContextAssemblyService(
    IMemoryStore memory,
    IKnowledgeRetriever retriever,
    ContextRankingEngine ranking,
    ContextEvictionPolicy eviction,
    TokenBudgetManager budget)
{
    public int MaxHistoryTurns { get; init; } = 20;

    public int RetrievalTopK { get; init; } = 8;

    public async Task<AssembledContext> AssembleAsync(ContextRequest request, CancellationToken cancellationToken)
    {
        // Retrieval runs alongside the memory reads; the two memory reads share one DbContext,
        // which does not allow concurrent operations, so they run one after the other.
        var knowledgeTask = retriever.RetrieveAsync(request.TenantId, request.BranchId, request.UserMessage, RetrievalTopK, cancellationToken);
        var history = await memory.GetRecentTurnsAsync(request.TenantId, request.ConversationId, MaxHistoryTurns, cancellationToken);
        var facts = await memory.GetFactsAsync(request.TenantId, request.UserId, cancellationToken);
        var knowledge = await knowledgeTask;

        var builder = new ContextBuilder().AddSystem("system", request.SystemInstructions);

        foreach (var live in request.LiveData)
        {
            builder.Add(live);
        }

        for (var i = 0; i < history.Count; i++)
        {
            // The latest exchange is pinned so the model never loses the thread.
            builder.AddHistory($"turn:{history[i].Id}", $"{history[i].Role}: {history[i].Content}", history[i].CreatedAt, pinned: i >= history.Count - 2);
        }

        foreach (var fact in facts)
        {
            builder.AddMemory($"fact:{fact.Id}", $"Customer {fact.Category}: {fact.Content}", fact.Confidence, fact.UpdatedAt);
        }

        foreach (var item in knowledge)
        {
            builder.AddRetrieved($"kb:{item.SourceType}:{item.SourceId}", item.Content, item.Similarity, $"{item.SourceType}:{item.SourceId}");
        }

        var result = eviction.Apply(ranking.Rank(builder.Items));
        return new AssembledContext(ToMessages(result.Included, request.UserMessage), result.Included, result.Evicted, result.TokensUsed, budget.TotalInputBudget);
    }

    private static List<ChatMessage> ToMessages(IReadOnlyList<ContextItem> included, string userMessage)
    {
        var messages = new List<ChatMessage>();

        var system = string.Join("\n\n", included.Where(i => i.Source == ContextSource.System).Select(i => i.Content));
        messages.Add(new ChatMessage(ChatRole.System, system));

        var grounding = included.Where(i => i.Source is ContextSource.LiveBusinessData or ContextSource.Memory or ContextSource.Retrieval).ToList();
        if (grounding.Count > 0)
        {
            var sections = grounding
                .GroupBy(i => i.Source)
                .Select(g => $"[{Label(g.Key)}]\n" + string.Join("\n", g.Select(i => i.Content)));
            messages.Add(new ChatMessage(ChatRole.System, string.Join("\n\n", sections)));
        }

        foreach (var turn in included.Where(i => i.Source == ContextSource.ConversationHistory))
        {
            var isAssistant = turn.Content.StartsWith($"{ConversationRole.Assistant}:", StringComparison.Ordinal);
            messages.Add(new ChatMessage(isAssistant ? ChatRole.Assistant : ChatRole.User, turn.Content[(turn.Content.IndexOf(':') + 1)..].TrimStart()));
        }

        messages.Add(new ChatMessage(ChatRole.User, userMessage));
        return messages;
    }

    private static string Label(ContextSource source) => source switch
    {
        ContextSource.LiveBusinessData => "Live restaurant data (authoritative)",
        ContextSource.Memory => "Known customer preferences",
        ContextSource.Retrieval => "Restaurant knowledge (may be outdated; live data wins)",
        _ => source.ToString(),
    };
}

public static class ContextOrchestratorServiceCollectionExtensions
{
    public static IServiceCollection AddContextOrchestrator(this IServiceCollection services, TokenBudgetOptions? options = null)
    {
        services.AddSingleton(options ?? new TokenBudgetOptions());
        services.AddSingleton<TokenBudgetManager>();
        services.AddSingleton<ContextRankingEngine>();
        services.AddSingleton<ContextEvictionPolicy>();
        services.AddScoped<ContextAssemblyService>();
        return services;
    }
}
