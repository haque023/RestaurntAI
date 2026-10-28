using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Restaurant.Domain;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Persistence;

public sealed class RestaurantDbContext(DbContextOptions<RestaurantDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string SchemaName = "restaurant";

    public DbSet<Domain.Restaurant> Restaurants => Set<Domain.Restaurant>();

    /// <summary>Public discovery read model; intentionally cross-tenant and without RLS.</summary>
    public DbSet<DiscoveryListingRow> DiscoveryListings => Set<DiscoveryListingRow>();

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
    public async Task SyncDiscoveryListingsAsync(Domain.Restaurant restaurant, TimeProvider timeProvider, CancellationToken cancellationToken)
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

    public void Apply(Domain.Restaurant restaurant, Branch branch, DateTimeOffset now)
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

internal sealed class RestaurantConfiguration : IEntityTypeConfiguration<Domain.Restaurant>
{
    public void Configure(EntityTypeBuilder<Domain.Restaurant> builder)
    {
        builder.ToTable("restaurants");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.DefaultCurrency).HasMaxLength(3).IsFixedLength();
        builder.Property(r => r.TimeZone).HasMaxLength(64);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
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
