using Microsoft.EntityFrameworkCore;

namespace RestaurantAI.Infrastructure.Idempotency;

public sealed class IdempotencyRepository<TContext>(TContext dbContext)
    where TContext : DbContext
{
    public Task<IdempotencyEntry?> FindAsync(Guid tenantId, string key, CancellationToken cancellationToken) =>
        dbContext.Set<IdempotencyEntry>()
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Key == key, cancellationToken);

    public void Add(IdempotencyEntry entry) => dbContext.Set<IdempotencyEntry>().Add(entry);

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.Set<IdempotencyEntry>().Where(e => e.ExpiresAt < now).ExecuteDeleteAsync(cancellationToken);
}

public static class IdempotencyModelBuilderExtensions
{
    public const string TableName = "idempotency_entries";

    public static ModelBuilder AddIdempotency(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyEntry>(builder =>
        {
            builder.ToTable(TableName);

            // The composite key doubles as the uniqueness guarantee for concurrent duplicates.
            builder.HasKey(e => new { e.TenantId, e.Key });
            builder.Property(e => e.Key).HasMaxLength(128);
            builder.Property(e => e.Operation).HasMaxLength(128).IsRequired();
            builder.Property(e => e.RequestHash).HasMaxLength(64).IsRequired();
            builder.Property(e => e.ResponseBody).HasColumnType("jsonb").IsRequired();
            builder.HasIndex(e => e.ExpiresAt);
        });

        return modelBuilder;
    }
}
