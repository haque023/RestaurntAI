using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.AI.Memory;

public enum ConversationRole
{
    User,
    Assistant,
    Tool,
}

/// <summary>Short-term memory: one turn of an AI conversation.</summary>
public sealed class ConversationTurn : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public ConversationRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public int TokenCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Long-term memory: a durable fact about a customer for a tenant (e.g. "prefers vegetarian").
/// Facts are hints for personalization, never authoritative business state.
/// </summary>
public sealed class MemoryFact : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}

public interface IMemoryStore
{
    Task AppendTurnAsync(ConversationTurn turn, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(Guid tenantId, Guid conversationId, int maxTurns, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemoryFact>> GetFactsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task UpsertFactAsync(MemoryFact fact, CancellationToken cancellationToken);
}
