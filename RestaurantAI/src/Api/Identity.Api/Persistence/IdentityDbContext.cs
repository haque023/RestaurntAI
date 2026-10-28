using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Identity.Domain;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "identity";

    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Users holding any membership in the tenant.</summary>
    public IQueryable<UserAccount> UsersInTenant(Guid tenantId) =>
        Users.Where(u => u.Memberships.Any(m => m.TenantId == tenantId));
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.ExternalSubject).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(u => u.ExternalSubject).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.OwnsMany(u => u.Memberships, m =>
        {
            m.ToJson("memberships");
            m.Property(x => x.TenantName).HasMaxLength(200);
        });
        builder.Ignore(u => u.DomainEvents);
    }
}
