using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
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

    public DbSet<AiUsageRecord> Usage => Set<AiUsageRecord>();

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

        modelBuilder.Entity<AiUsageRecord>(builder =>
        {
            builder.ToTable("ai_usage");
            builder.Property(u => u.Agent).HasMaxLength(64);
            builder.Property(u => u.Model).HasMaxLength(64);
            builder.Property(u => u.Outcome).HasMaxLength(32);
            builder.HasIndex(u => new { u.TenantId, u.CreatedAt });
            builder.HasIndex(u => u.CreatedAt);
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Conversations without a restaurant (platform discovery) have no tenant to own their rows, so
/// their short-term history lives in the distributed cache with a sliding expiry instead.
/// </summary>
internal sealed class RoutingMemoryStore(PostgresMemoryStore tenantStore, IDistributedCache cache) : IMemoryStore
{
    private static readonly DistributedCacheEntryOptions Expiry = new() { SlidingExpiration = TimeSpan.FromHours(2) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int MaxCachedTurns = 40;

    public async Task AppendTurnAsync(ConversationTurn turn, CancellationToken cancellationToken)
    {
        if (turn.TenantId != Guid.Empty)
        {
            await tenantStore.AppendTurnAsync(turn, cancellationToken);
            return;
        }

        var turns = await ReadAsync(turn.UserId, turn.ConversationId, cancellationToken);
        turns.Add(turn);
        await cache.SetStringAsync(Key(turn.UserId, turn.ConversationId), JsonSerializer.Serialize(turns.TakeLast(MaxCachedTurns), Json), Expiry, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(Guid tenantId, Guid userId, Guid conversationId, int maxTurns, CancellationToken cancellationToken) =>
        tenantId != Guid.Empty
            ? await tenantStore.GetRecentTurnsAsync(tenantId, userId, conversationId, maxTurns, cancellationToken)
            : (await ReadAsync(userId, conversationId, cancellationToken)).TakeLast(maxTurns).ToList();

    public Task<IReadOnlyList<MemoryFact>> GetFactsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        tenantId != Guid.Empty ? tenantStore.GetFactsAsync(tenantId, userId, cancellationToken) : Task.FromResult<IReadOnlyList<MemoryFact>>([]);

    public Task UpsertFactAsync(MemoryFact fact, CancellationToken cancellationToken) =>
        fact.TenantId != Guid.Empty ? tenantStore.UpsertFactAsync(fact, cancellationToken) : Task.CompletedTask;

    private async Task<List<ConversationTurn>> ReadAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var raw = await cache.GetStringAsync(Key(userId, conversationId), cancellationToken);
        return raw is null ? [] : JsonSerializer.Deserialize<List<ConversationTurn>>(raw, Json) ?? [];
    }

    private static string Key(Guid userId, Guid conversationId) => $"conversation:{userId:N}:{conversationId:N}";
}

internal sealed class PostgresMemoryStore(MemoryDbContext dbContext, TimeProvider timeProvider) : IMemoryStore
{
    public async Task AppendTurnAsync(ConversationTurn turn, CancellationToken cancellationToken)
    {
        dbContext.Turns.Add(turn);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(Guid tenantId, Guid userId, Guid conversationId, int maxTurns, CancellationToken cancellationToken)
    {
        var turns = await dbContext.Turns.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.UserId == userId && t.ConversationId == conversationId)
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
        services.AddScoped<PostgresMemoryStore>();
        services.AddScoped<RoutingMemoryStore>();
        services.AddScoped<IMemoryStore>(provider => provider.GetRequiredService<RoutingMemoryStore>());
        return services;
    }
}
