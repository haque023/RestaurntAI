using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Data;

public sealed class RestaurantDbContext(DbContextOptions<RestaurantDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "restaurant";

    public DbSet<Models.Restaurant> Restaurants => Set<Models.Restaurant>();

    /// <summary>Public discovery read model; intentionally cross-tenant and without RLS.</summary>
    public DbSet<DiscoveryListingRow> DiscoveryListings => Set<DiscoveryListingRow>();

    public DbSet<DiningTable> Tables => Set<DiningTable>();

    /// <summary>Public QR lookup (token → restaurant/branch/table); holds no confidential data, no RLS.</summary>
    public DbSet<QrCodeRow> QrCodes => Set<QrCodeRow>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Brings the discovery rows for a restaurant in line with its current state. Call before
    /// SaveChanges so the read model is committed in the same transaction as the change.
    /// </summary>
    public async Task SyncDiscoveryListingsAsync(Models.Restaurant restaurant, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var existing = await DiscoveryListings.Where(l => l.TenantId == restaurant.Id).ToDictionaryAsync(l => l.BranchId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var wanted = restaurant.IsListed ? restaurant.Branches.ToDictionary(b => b.Id) : [];

        foreach (var (branchId, row) in existing)
        {
            if (!wanted.ContainsKey(branchId))
            {
                DiscoveryListings.Remove(row);
            }
        }

        foreach (var branch in wanted.Values)
        {
            if (!existing.TryGetValue(branch.Id, out var row))
            {
                row = new DiscoveryListingRow { BranchId = branch.Id, TenantId = restaurant.Id };
                DiscoveryListings.Add(row);
            }

            row.Apply(restaurant, branch, now);
        }
    }
}

/// <summary>
/// One listed branch with only public data. Not <see cref="SharedKernel.Domain.ITenantOwned"/>:
/// it is read anonymously across tenants by discovery and holds nothing confidential.
/// </summary>
public sealed class DiscoveryListingRow
{
    public Guid BranchId { get; set; }

    public Guid TenantId { get; set; }

    public string RestaurantName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Cuisines { get; set; } = [];

    public List<string> DietaryOptions { get; set; } = [];

    public string BranchName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string TimeZone { get; set; } = "UTC";

    public List<OpeningPeriod> OpeningHours { get; set; } = [];

    /// <summary>Lower-cased name, branch, description and cuisines for simple text search.</summary>
    public string SearchText { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public void Apply(Models.Restaurant restaurant, Branch branch, DateTimeOffset now)
    {
        RestaurantName = restaurant.Name;
        Description = restaurant.Description;
        Cuisines = [.. restaurant.Cuisines];
        DietaryOptions = [.. restaurant.DietaryOptions];
        BranchName = branch.Name;
        Address = branch.Address;
        Latitude = branch.Latitude;
        Longitude = branch.Longitude;
        TimeZone = restaurant.TimeZone;
        OpeningHours = [.. branch.OpeningHours];
        SearchText = string.Join(' ', [restaurant.Name, branch.Name, restaurant.Description, .. restaurant.Cuisines]).ToLowerInvariant();
        UpdatedAt = now;
    }
}

/// <summary>Maps a printed table QR token to its venue. Written in the same transaction as the table.</summary>
public sealed class QrCodeRow
{
    public string Token { get; set; } = string.Empty;

    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid TableId { get; set; }

    public string TableLabel { get; set; } = string.Empty;
}

internal static class OpeningHoursMapping
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Stores opening hours as a jsonb document.</summary>
    public static PropertyBuilder<List<OpeningPeriod>> HasOpeningHoursConversion(this PropertyBuilder<List<OpeningPeriod>> property) =>
        property
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<OpeningPeriod>>(v, Json) ?? new List<OpeningPeriod>(),
                new ValueComparer<List<OpeningPeriod>>(
                    (a, b) => (a ?? new List<OpeningPeriod>()).SequenceEqual(b ?? new List<OpeningPeriod>()),
                    v => v.Aggregate(0, (hash, p) => HashCode.Combine(hash, p.GetHashCode())),
                    v => v.ToList()));
}

internal sealed class RestaurantConfiguration : IEntityTypeConfiguration<Models.Restaurant>
{
    public void Configure(EntityTypeBuilder<Models.Restaurant> builder)
    {
        builder.ToTable("restaurants");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.DefaultCurrency).HasMaxLength(3).IsFixedLength();
        builder.Property(r => r.TimeZone).HasMaxLength(64);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Phone).HasMaxLength(40);
        builder.Property(r => r.Website).HasMaxLength(300);
        builder.Property(r => r.PlanCode).HasMaxLength(32).HasDefaultValue("starter");
        builder.Property(r => r.AiEnabled).HasDefaultValue(true);
        builder.Property(r => r.AiKnowledge).HasMaxLength(Models.Restaurant.MaxAiKnowledgeLength).HasDefaultValue(string.Empty);
        builder.Property(r => r.AiTone).HasMaxLength(200).HasDefaultValue(string.Empty);
        builder.Property(r => r.SuspensionReason).HasMaxLength(500);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        builder.Ignore(r => r.Plan);
        builder.HasMany(r => r.Branches).WithOne().HasForeignKey(b => b.TenantId);
        builder.Navigation(r => r.Branches).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(r => r.IsListed);
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Address).HasMaxLength(500);
        builder.Property(b => b.OpeningHours).HasOpeningHoursConversion();
    }
}

internal sealed class DiscoveryListingConfiguration : IEntityTypeConfiguration<DiscoveryListingRow>
{
    public void Configure(EntityTypeBuilder<DiscoveryListingRow> builder)
    {
        builder.ToTable("discovery_listings");
        builder.HasKey(l => l.BranchId);
        builder.Property(l => l.RestaurantName).HasMaxLength(200);
        builder.Property(l => l.BranchName).HasMaxLength(200);
        builder.Property(l => l.Description).HasMaxLength(2000);
        builder.Property(l => l.Address).HasMaxLength(500);
        builder.Property(l => l.TimeZone).HasMaxLength(64);
        builder.Property(l => l.SearchText).HasMaxLength(4000);
        builder.Property(l => l.OpeningHours).HasOpeningHoursConversion();
        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => new { l.Latitude, l.Longitude });
        builder.HasIndex(l => l.Cuisines).HasMethod("gin");
        builder.HasIndex(l => l.DietaryOptions).HasMethod("gin");
    }
}

internal sealed class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        builder.ToTable("dining_tables");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Label).HasMaxLength(40).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.QrToken).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.QrToken).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.BranchId, t.Label }).IsUnique();
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class QrCodeConfiguration : IEntityTypeConfiguration<QrCodeRow>
{
    public void Configure(EntityTypeBuilder<QrCodeRow> builder)
    {
        builder.ToTable("qr_codes");
        builder.HasKey(q => q.Token);
        builder.Property(q => q.Token).HasMaxLength(64);
        builder.Property(q => q.TableLabel).HasMaxLength(40);
        builder.HasIndex(q => q.TableId).IsUnique();
    }
}
