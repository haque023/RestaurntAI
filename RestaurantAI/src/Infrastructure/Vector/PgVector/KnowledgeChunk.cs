using Pgvector;
using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Infrastructure.Vector.PgVector;

public sealed class KnowledgeChunk : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? BranchId { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public Pgvector.Vector Embedding { get; set; } = new(Array.Empty<float>());

    public Dictionary<string, string> Metadata { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }
}
