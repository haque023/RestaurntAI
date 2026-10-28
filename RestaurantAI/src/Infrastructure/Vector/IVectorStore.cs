namespace RestaurantAI.Infrastructure.Vector;

public sealed record VectorRecord(
    Guid Id,
    Guid TenantId,
    Guid? BranchId,
    string SourceType,
    string SourceId,
    string Content,
    ReadOnlyMemory<float> Embedding,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record VectorMatch(VectorRecord Record, double Similarity);

/// <summary>
/// Tenant-aware embedding storage for static knowledge (menu descriptions, policies, FAQs).
/// Live transactional data (price, availability, order state) must never be served from here.
/// </summary>
public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyCollection<VectorRecord> records, CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorMatch>> SearchAsync(
        Guid tenantId,
        Guid? branchId,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken);

    Task DeleteBySourceAsync(Guid tenantId, string sourceType, string sourceId, CancellationToken cancellationToken);
}
