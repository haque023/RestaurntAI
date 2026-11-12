using Microsoft.EntityFrameworkCore;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Infrastructure.Vector.PgVector;

public sealed class VectorDbContext(DbContextOptions<VectorDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "knowledge";

    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KnowledgeChunk>(builder =>
        {
            builder.ToTable("knowledge_chunks");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.SourceType).HasMaxLength(64);
            builder.Property(c => c.SourceId).HasMaxLength(128);
            builder.Property(c => c.Embedding).HasColumnType($"vector({PgVectorOptions.DefaultDimensions})");
            builder.Property(c => c.Metadata).HasColumnType("jsonb");
            builder.HasIndex(c => new { c.TenantId, c.SourceType, c.SourceId });
            builder.HasIndex(c => c.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });

        base.OnModelCreating(modelBuilder);
    }
}

public sealed class PgVectorOptions
{
    public const string SectionName = "Vector";

    /// <summary>Embedding size; must match the embedding model deployed in Microsoft Foundry.</summary>
    public const int DefaultDimensions = 1536;
}
