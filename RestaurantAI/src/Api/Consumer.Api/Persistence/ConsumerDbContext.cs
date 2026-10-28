using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Consumer.Domain;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Consumer.Api.Persistence;

public sealed class ConsumerDbContext(DbContextOptions<ConsumerDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "consumer";

    public DbSet<CustomerProfile> Profiles => Set<CustomerProfile>();

    public DbSet<FavoriteRestaurant> Favorites => Set<FavoriteRestaurant>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("customer_profiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.DisplayName).HasMaxLength(200);
        builder.Property(p => p.PreferredLanguage).HasMaxLength(16);
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class FavoriteRestaurantConfiguration : IEntityTypeConfiguration<FavoriteRestaurant>
{
    public void Configure(EntityTypeBuilder<FavoriteRestaurant> builder)
    {
        builder.ToTable("favorite_restaurants");
        builder.HasKey(f => new { f.UserId, f.RestaurantId });
        builder.HasIndex(f => new { f.UserId, f.SavedAt });
    }
}
