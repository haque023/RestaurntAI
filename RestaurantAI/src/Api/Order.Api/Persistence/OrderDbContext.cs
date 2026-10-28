using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Persistence;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "orders";

    public DbSet<Domain.Order> Orders => Set<Domain.Order>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        modelBuilder.AddIdempotency();
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Domain.Order>
{
    public void Configure(EntityTypeBuilder<Domain.Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.Channel).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(o => o.Total).HasPrecision(12, 2);
        builder.Property(o => o.Notes).HasMaxLength(500);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.Property(o => o.Version).IsRowVersion();

        builder.OwnsMany(o => o.Lines, lines =>
        {
            lines.ToJson("lines");
            lines.Property(l => l.UnitPrice).HasPrecision(12, 2);
            lines.Ignore(l => l.LineTotal);
        });

        builder.HasIndex(o => new { o.TenantId, o.BranchId, o.Status });
        builder.HasIndex(o => new { o.TenantId, o.CustomerId, o.PlacedAt });
        builder.Ignore(o => o.DomainEvents);
    }
}
