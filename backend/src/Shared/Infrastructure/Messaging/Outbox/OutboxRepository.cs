using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Infrastructure.Messaging.Outbox;

public sealed class OutboxRepository<TContext>(TContext dbContext)
    where TContext : DbContext
{
    public void Add(OutboxMessage message) => dbContext.Set<OutboxMessage>().Add(message);

    /// <summary>
    /// Locks a batch of unprocessed messages. <c>SKIP LOCKED</c> lets several dispatcher
    /// instances run concurrently without publishing the same message twice in parallel.
    /// Must be called inside a transaction.
    /// </summary>
    public Task<List<OutboxMessage>> LockPendingBatchAsync(int batchSize, int maxAttempts, CancellationToken cancellationToken)
    {
        // Table name comes from the EF model (not user input); values are parameterized.
        var entity = dbContext.Model.FindEntityType(typeof(OutboxMessage))!;
        var sql = $$"""
            SELECT * FROM "{{entity.GetSchema()}}"."{{entity.GetTableName()}}"
            WHERE processed_at IS NULL AND attempts < {0}
            ORDER BY occurred_at
            LIMIT {1}
            FOR UPDATE SKIP LOCKED
            """;

        return dbContext.Set<OutboxMessage>()
            .FromSqlRaw(sql, maxAttempts, batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

public static class OutboxModelBuilderExtensions
{
    public const string TableName = "outbox_messages";

    /// <summary>Adds the outbox table to a module's schema.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable(TableName);
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).HasMaxLength(256).IsRequired();
            builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(m => m.CorrelationId).HasMaxLength(128);
            builder.HasIndex(m => m.OccurredAt).HasFilter("processed_at IS NULL");
        });

        return modelBuilder;
    }

    internal static IEnumerable<IHasDomainEvents> AggregatesWithEvents(this DbContext dbContext) =>
        dbContext.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0);
}
