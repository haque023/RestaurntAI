using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.AI.Memory;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "ai_memory";

    public DbSet<ConversationTurn> Turns => Set<ConversationTurn>();

    public DbSet<MemoryFact> Facts => Set<MemoryFact>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationTurn>(builder =>
        {
            builder.ToTable("conversation_turns");
            builder.Property(t => t.Role).HasConversion<string>().HasMaxLength(16);
            builder.HasIndex(t => new { t.TenantId, t.ConversationId, t.CreatedAt });
        });

        modelBuilder.Entity<MemoryFact>(builder =>
        {
            builder.ToTable("memory_facts");
            builder.Property(f => f.Category).HasMaxLength(64);
            builder.Property(f => f.Content).HasMaxLength(1000);
            builder.HasIndex(f => new { f.TenantId, f.UserId, f.Category });
        });

        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class PostgresMemoryStore(MemoryDbContext dbContext, TimeProvider timeProvider) : IMemoryStore
{
    public async Task AppendTurnAsync(ConversationTurn turn, CancellationToken cancellationToken)
    {
        dbContext.Turns.Add(turn);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(Guid tenantId, Guid conversationId, int maxTurns, CancellationToken cancellationToken)
    {
        var turns = await dbContext.Turns.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ConversationId == conversationId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(maxTurns)
            .ToListAsync(cancellationToken);

        turns.Reverse();
        return turns;
    }

    public async Task<IReadOnlyList<MemoryFact>> GetFactsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await dbContext.Facts.AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.UserId == userId && (f.ExpiresAt == null || f.ExpiresAt > now))
            .OrderByDescending(f => f.Confidence)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertFactAsync(MemoryFact fact, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Facts.SingleOrDefaultAsync(
            f => f.TenantId == fact.TenantId && f.UserId == fact.UserId && f.Category == fact.Category && f.Content == fact.Content,
            cancellationToken);

        if (existing is null)
        {
            dbContext.Facts.Add(fact);
        }
        else
        {
            existing.Confidence = Math.Max(existing.Confidence, fact.Confidence);
            existing.UpdatedAt = timeProvider.GetUtcNow();
            existing.ExpiresAt = fact.ExpiresAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class MemoryServiceCollectionExtensions
{
    public static IServiceCollection AddAiMemory(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgresDbContext<MemoryDbContext>(configuration, MemoryDbContext.SchemaName);
        services.AddScoped<IMemoryStore, PostgresMemoryStore>();
        return services;
    }
}
