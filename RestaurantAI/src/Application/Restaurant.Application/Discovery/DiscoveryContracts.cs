using RestaurantAI.Restaurant.Domain;

namespace RestaurantAI.Restaurant.Application.Discovery;

/// <summary>Public data of one listed branch, as stored in the discovery read model.</summary>
public sealed record DiscoveryListing(
    Guid TenantId,
    Guid BranchId,
    string RestaurantName,
    string Description,
    IReadOnlyList<string> Cuisines,
    IReadOnlyList<string> DietaryOptions,
    string BranchName,
    string Address,
    double? Latitude,
    double? Longitude,
    string TimeZone,
    IReadOnlyList<OpeningPeriod> OpeningHours);

public sealed record DiscoveryQuery
{
    public const int MaxLimit = 100;
    public const double MaxRadiusKm = 100;

    public string? Text { get; init; }

    public IReadOnlyCollection<string> Cuisines { get; init; } = [];

    public IReadOnlyCollection<string> Diets { get; init; } = [];

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double RadiusKm { get; init; } = 10;

    public bool OpenNow { get; init; }

    public IReadOnlyCollection<Guid> BranchIds { get; init; } = [];

    public IReadOnlyCollection<Guid> TenantIds { get; init; } = [];

    public int Limit { get; init; } = 30;

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;
}

public sealed record DiscoveryResult(
    Guid TenantId,
    Guid BranchId,
    string RestaurantName,
    string Description,
    IReadOnlyList<string> Cuisines,
    IReadOnlyList<string> DietaryOptions,
    string BranchName,
    string Address,
    double? Latitude,
    double? Longitude,
    double? DistanceKm,
    bool IsOpenNow,
    IReadOnlyList<OpeningPeriod> OpeningHours);
