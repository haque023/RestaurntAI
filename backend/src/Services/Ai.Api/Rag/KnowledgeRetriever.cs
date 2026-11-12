using Microsoft.Extensions.AI;
using RestaurantAI.Infrastructure.Vector;

namespace RestaurantAI.AI.Rag;

public sealed record RetrievedKnowledge(string SourceType, string SourceId, string Content, double Similarity);

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<RetrievedKnowledge>> RetrieveAsync(Guid tenantId, Guid? branchId, string query, int topK, CancellationToken cancellationToken);
}

/// <summary>
/// Tenant-aware retrieval of static knowledge (policies, FAQs, menu descriptions).
/// Results are context for the model; live prices and availability come from MCP tools.
/// </summary>
public sealed class KnowledgeRetriever(
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IVectorStore vectorStore) : IKnowledgeRetriever
{
    public const double MinimumSimilarity = 0.25;

    public async Task<IReadOnlyList<RetrievedKnowledge>> RetrieveAsync(
        Guid tenantId,
        Guid? branchId,
        string query,
        int topK,
        CancellationToken cancellationToken)
    {
        var embedding = await embeddings.GenerateVectorAsync(query, cancellationToken: cancellationToken);
        var matches = await vectorStore.SearchAsync(tenantId, branchId, embedding, topK, cancellationToken);

        return matches
            .Where(m => m.Similarity >= MinimumSimilarity)
            .Select(m => new RetrievedKnowledge(m.Record.SourceType, m.Record.SourceId, m.Record.Content, m.Similarity))
            .ToList();
    }
}

/// <summary>
/// Used when no embedding provider is configured (Claude has no embeddings API): the agent then
/// answers from live tool data and its instructions only. Register a real
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> and <see cref="RagServiceCollectionExtensions.AddRag"/> to enable retrieval.
/// </summary>
public sealed class NoKnowledgeRetriever : IKnowledgeRetriever
{
    public Task<IReadOnlyList<RetrievedKnowledge>> RetrieveAsync(Guid tenantId, Guid? branchId, string query, int topK, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RetrievedKnowledge>>([]);
}
