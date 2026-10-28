using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Reservation.Application.Reservations;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Reservation.Api.Persistence;

public sealed class ReservationDbContext(DbContextOptions<ReservationDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "reservations";

    /// <summary>Exclusion constraint created in the initial migration (prevents double booking).</summary>
    public const string NoOverlapConstraint = "reservations_no_table_overlap";

    public DbSet<Domain.Reservation> Reservations => Set<Domain.Reservation>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.AddOutbox();
        modelBuilder.AddIdempotency();
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Domain.Reservation>
{
    public void Configure(EntityTypeBuilder<Domain.Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.SpecialRequests).HasMaxLength(1000);
        builder.Property(r => r.CancellationReason).HasMaxLength(500);
        builder.HasIndex(r => new { r.TenantId, r.BranchId, r.StartsAt });
        builder.HasIndex(r => new { r.TenantId, r.CustomerId });
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class ReservationRepository(ReservationDbContext dbContext) : IReservationRepository
{
    public void Add(Domain.Reservation reservation) => dbContext.Reservations.Add(reservation);

    public Task<Domain.Reservation?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Reservations.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
}
