namespace RestaurantAI.AI.Context;

/// <summary>Where a piece of context came from. Order reflects default authority.</summary>
public enum ContextSource
{
    /// <summary>Agent instructions and safety rules. Always included.</summary>
    System,

    /// <summary>Authoritative data returned by business APIs (price, availability, order state).</summary>
    LiveBusinessData,

    /// <summary>Recent conversation turns.</summary>
    ConversationHistory,

    /// <summary>Long-term customer memory (preferences).</summary>
    Memory,

    /// <summary>Static knowledge retrieved via RAG (policies, FAQs, descriptions).</summary>
    Retrieval,
}

/// <summary>
/// A candidate piece of context for a model call.
/// </summary>
/// <param name="Id">Stable id, used to report evictions.</param>
/// <param name="Source">Origin of the content.</param>
/// <param name="Content">Text sent to the model.</param>
/// <param name="Relevance">0..1 relevance to the current request (similarity for RAG, 1 for live data).</param>
/// <param name="Timestamp">When the information was produced; used for recency decay.</param>
/// <param name="EntityKey">Business entity this item describes (e.g. <c>menu-item:{id}</c>). Live data supersedes retrieval for the same key.</param>
/// <param name="Pinned">Pinned items are never evicted.</param>
public sealed record ContextItem(
    string Id,
    ContextSource Source,
    string Content,
    double Relevance = 1,
    DateTimeOffset? Timestamp = null,
    string? EntityKey = null,
    bool Pinned = false)
{
    public int Tokens { get; init; } = TokenBudgetManager.EstimateTokens(Content);
}

public sealed record RankedContextItem(ContextItem Item, double Score);

public sealed record EvictedContextItem(ContextItem Item, string Reason);
