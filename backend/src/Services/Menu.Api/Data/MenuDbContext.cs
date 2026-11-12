using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Menu.Api.Data;

public sealed class MenuDbContext(DbContextOptions<MenuDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "menu";

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    /// <summary>Public dish read model for cross-restaurant discovery; intentionally without RLS.</summary>
    public DbSet<PublicMenuItemRow> PublicMenuItems => Set<PublicMenuItemRow>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Keeps the public dish read model in the same transaction as every menu change.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var changed = ChangeTracker.Entries<MenuItem>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        if (changed.Count > 0)
        {
            var ids = changed.Select(i => i.Id).ToList();
            var existing = await PublicMenuItems.Where(p => ids.Contains(p.MenuItemId)).ToDictionaryAsync(p => p.MenuItemId, cancellationToken);
            foreach (var item in changed)
            {
                if (!existing.TryGetValue(item.Id, out var row))
                {
                    row = new PublicMenuItemRow { MenuItemId = item.Id };
                    PublicMenuItems.Add(row);
                }

                row.Apply(item, DateTimeOffset.UtcNow);
            }
        }

        var deleted = ChangeTracker.Entries<MenuItem>().Where(e => e.State == EntityState.Deleted).Select(e => e.Entity.Id).ToList();
        if (deleted.Count > 0)
        {
            PublicMenuItems.RemoveRange(await PublicMenuItems.Where(p => deleted.Contains(p.MenuItemId)).ToListAsync(cancellationToken));
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Public copy of a menu item for dish search across restaurants. Not ITenantOwned: menus are
/// public (anyone at a QR code sees them) and this row holds nothing confidential.
/// </summary>
public sealed class PublicMenuItemRow
{
    public Guid MenuItemId { get; set; }

    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> DietaryTags { get; set; } = [];

    public decimal Price { get; set; }

    public string Currency { get; set; } = "USD";

    public bool IsAvailable { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public void Apply(MenuItem item, DateTimeOffset now)
    {
        TenantId = item.TenantId;
        BranchId = item.BranchId;
        Name = item.Name;
        Category = item.Category;
        Description = item.Description;
        DietaryTags = [.. item.DietaryTags];
        Price = item.Price;
        Currency = item.Currency;
        IsAvailable = item.IsAvailable;
        SearchText = string.Join(' ', [item.Name, item.Category, item.Description, .. item.DietaryTags, .. item.Ingredients]).ToLowerInvariant();
        UpdatedAt = now;
    }
}

internal sealed class PublicMenuItemConfiguration : IEntityTypeConfiguration<PublicMenuItemRow>
{
    public void Configure(EntityTypeBuilder<PublicMenuItemRow> builder)
    {
        builder.ToTable("public_menu_items");
        builder.HasKey(p => p.MenuItemId);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Price).HasPrecision(12, 2);
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.SearchText).HasMaxLength(4000);
        builder.HasIndex(p => p.BranchId);
        builder.HasIndex(p => p.DietaryTags).HasMethod("gin");
    }
}

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Category).HasMaxLength(100);
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(2000);
        builder.Property(i => i.Price).HasPrecision(12, 2);
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength();
        builder.HasIndex(i => new { i.TenantId, i.BranchId, i.Category });
        builder.Ignore(i => i.UnitPrice);
        builder.Ignore(i => i.DomainEvents);
    }
}
