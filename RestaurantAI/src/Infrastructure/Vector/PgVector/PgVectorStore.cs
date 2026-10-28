using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace RestaurantAI.Infrastructure.Vector.PgVector;

internal sealed class PgVectorStore(VectorDbContext dbContext, TimeProvider timeProvider) : IVectorStore
{
    public async Task UpsertAsync(IReadOnlyCollection<VectorRecord> records, CancellationToken cancellationToken)
    {
        var ids = records.Select(r => r.Id).ToList();
        var existing = await dbContext.KnowledgeChunks.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var record in records)
        {
            if (!existing.TryGetValue(record.Id, out var chunk))
            {
                chunk = new KnowledgeChunk { Id = record.Id };
                dbContext.KnowledgeChunks.Add(chunk);
            }

            chunk.TenantId = record.TenantId;
            chunk.BranchId = record.BranchId;
            chunk.SourceType = record.SourceType;
            chunk.SourceId = record.SourceId;
            chunk.Content = record.Content;
            chunk.Embedding = new Pgvector.Vector(record.Embedding);
            chunk.Metadata = new Dictionary<string, string>(record.Metadata);
            chunk.UpdatedAt = timeProvider.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(
        Guid tenantId,
        Guid? branchId,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var query = new Pgvector.Vector(queryEmbedding);

        // Tenant predicate is explicit in addition to the query filter and RLS.
        var matches = await dbContext.KnowledgeChunks
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && (branchId == null || c.BranchId == null || c.BranchId == branchId))
            .Select(c => new { Chunk = c, Distance = c.Embedding.CosineDistance(query) })
            .OrderBy(x => x.Distance)
            .Take(topK)
            .ToListAsync(cancellationToken);

        return matches
            .Select(m => new VectorMatch(
                new VectorRecord(m.Chunk.Id, m.Chunk.TenantId, m.Chunk.BranchId, m.Chunk.SourceType, m.Chunk.SourceId, m.Chunk.Content, m.Chunk.Embedding.Memory, m.Chunk.Metadata),
                1 - m.Distance))
            .ToList();
    }

    public Task DeleteBySourceAsync(Guid tenantId, string sourceType, string sourceId, CancellationToken cancellationToken) =>
        dbContext.KnowledgeChunks
            .Where(c => c.TenantId == tenantId && c.SourceType == sourceType && c.SourceId == sourceId)
            .ExecuteDeleteAsync(cancellationToken);
}
