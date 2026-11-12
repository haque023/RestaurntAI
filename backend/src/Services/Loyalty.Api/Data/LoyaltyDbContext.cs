using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Loyalty.Api.Data;

public sealed class LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "loyalty";

    public DbSet<LoyaltyAccount> Accounts => Set<LoyaltyAccount>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        builder.ToTable("loyalty_accounts");
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.TenantId, a.CustomerId }).IsUnique();
        builder.OwnsMany(a => a.Transactions, t =>
        {
            t.ToTable("loyalty_transactions");
            t.WithOwner().HasForeignKey("AccountId");
            t.HasKey(x => x.Id);
            t.Property(x => x.SourceType).HasMaxLength(64);
            t.HasIndex("AccountId", nameof(LoyaltyTransaction.SourceType), nameof(LoyaltyTransaction.SourceId)).IsUnique();
        });
        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class LoyaltyAccountRepository(LoyaltyDbContext dbContext) : ILoyaltyAccountRepository
{
    public void Add(LoyaltyAccount account) => dbContext.Accounts.Add(account);

    public Task<LoyaltyAccount?> FindByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Accounts.SingleOrDefaultAsync(a => a.TenantId == tenantId && a.CustomerId == customerId, cancellationToken);
}
