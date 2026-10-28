using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Admin.Api.Persistence;

/// <summary>Platform administration data: the cross-tenant audit log built from the event bus.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "platform";

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override string Schema => SchemaName;
}

/// <summary>
/// One platform event (domain events from every module, AI tool executions). The message id is the
/// key, so redelivered messages are stored once (idempotent consumer).
/// </summary>
public sealed class AuditEvent
{
    public Guid Id { get; set; }

    /// <summary>e.g. <c>order.OrderCreated</c>, <c>mcp.ToolExecuted</c>.</summary>
    public string Type { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public Guid? ActorId { get; set; }

    public string? CorrelationId { get; set; }

    public string Payload { get; set; } = "{}";

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(256);
        builder.Property(e => e.Module).HasMaxLength(64);
        builder.Property(e => e.CorrelationId).HasMaxLength(128);
        builder.Property(e => e.Payload).HasColumnType("jsonb");
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });
        builder.HasIndex(e => new { e.Type, e.OccurredAt });
    }
}
