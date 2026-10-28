using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.Infrastructure.Vector;

namespace RestaurantAI.AI.Rag;

public sealed record KnowledgeDocument(Guid TenantId, Guid? BranchId, string SourceType, string SourceId, string Text, IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// Chunks and embeds knowledge documents. Triggered by events such as MenuItemChanged or a
/// knowledge document upload; re-indexing a source replaces its previous chunks.
/// </summary>
public sealed class KnowledgeIndexer(IEmbeddingGenerator<string, Embedding<float>> embeddings, IVectorStore vectorStore)
{
    public const int ChunkSize = 1200;
    public const int ChunkOverlap = 200;

    public async Task IndexAsync(KnowledgeDocument document, CancellationToken cancellationToken)
    {
        await vectorStore.DeleteBySourceAsync(document.TenantId, document.SourceType, document.SourceId, cancellationToken);

        var chunks = Chunk(document.Text).ToList();
        if (chunks.Count == 0)
        {
            return;
        }

        var vectors = await embeddings.GenerateAsync(chunks, cancellationToken: cancellationToken);
        var records = chunks
            .Select((chunk, index) => new VectorRecord(
                DeterministicId(document, index),
                document.TenantId,
                document.BranchId,
                document.SourceType,
                document.SourceId,
                chunk,
                vectors[index].Vector,
                document.Metadata))
            .ToList();

        await vectorStore.UpsertAsync(records, cancellationToken);
    }

    public static IEnumerable<string> Chunk(string text)
    {
        var normalized = text.Trim();
        for (var start = 0; start < normalized.Length; start += ChunkSize - ChunkOverlap)
        {
            var length = Math.Min(ChunkSize, normalized.Length - start);
            yield return normalized.Substring(start, length);
            if (start + length >= normalized.Length)
            {
                yield break;
            }
        }
    }

    private static Guid DeterministicId(KnowledgeDocument document, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{document.TenantId}|{document.SourceType}|{document.SourceId}|{index}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}

public static class RagServiceCollectionExtensions
{
    /// <summary>Requires an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> (registered by Foundry.Integration).</summary>
    public static IServiceCollection AddRag(this IServiceCollection services)
    {
        services.AddScoped<IKnowledgeRetriever, KnowledgeRetriever>();
        services.AddScoped<KnowledgeIndexer>();
        return services;
    }
}
