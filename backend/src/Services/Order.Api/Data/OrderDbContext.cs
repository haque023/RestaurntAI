using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Data;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "orders";

    public DbSet<Models.Order> Orders => Set<Models.Order>();

    public DbSet<Models.ServiceRequest> ServiceRequests => Set<Models.ServiceRequest>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        modelBuilder.AddIdempotency();
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Models.Order>
{
    public void Configure(EntityTypeBuilder<Models.Order> builder)
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

        builder.OwnsMany(o => o.History, history =>
        {
            history.ToJson("history");
            history.Property(h => h.Status).HasConversion<string>();
        });
        builder.Property(o => o.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(o => new { o.TenantId, o.BranchId, o.Status });
        builder.HasIndex(o => new { o.TenantId, o.BranchId, o.PlacedAt });
        builder.HasIndex(o => new { o.TenantId, o.CustomerId, o.PlacedAt });
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<Models.ServiceRequest>
{
    public void Configure(EntityTypeBuilder<Models.ServiceRequest> builder)
    {
        builder.ToTable("service_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Note).HasMaxLength(Models.ServiceRequest.MaxNoteLength);
        builder.HasIndex(r => new { r.TenantId, r.BranchId, r.Status });
        builder.Ignore(r => r.DomainEvents);
    }
}
