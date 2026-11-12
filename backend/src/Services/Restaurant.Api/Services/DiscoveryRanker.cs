
namespace RestaurantAI.Restaurant.Api.Services;

/// <summary>
/// Final filtering and ordering of discovery candidates: exact radius, "open now" in each
/// restaurant's own time zone, then nearest first (or by name without a location).
/// Coarse filters (text, cuisine, diet, bounding box) run in the database beforehand.
/// </summary>
public static class DiscoveryRanker
{
    public static IReadOnlyList<DiscoveryResult> Apply(IEnumerable<DiscoveryListing> candidates, DiscoveryQuery query, DateTimeOffset utcNow)
    {
        var results = new List<DiscoveryResult>();

        foreach (var listing in candidates)
        {
            double? distance = query.HasLocation && listing.Latitude is { } lat && listing.Longitude is { } lon
                ? GeoDistance.Kilometres(query.Latitude!.Value, query.Longitude!.Value, lat, lon)
                : null;

            if (query.HasLocation && (distance is null || distance > query.RadiusKm))
            {
                continue;
            }

            var isOpen = OpeningHoursCalculator.IsOpen(listing.OpeningHours, OpeningHoursCalculator.ToLocal(utcNow, listing.TimeZone));
            if (query.OpenNow && !isOpen)
            {
                continue;
            }

            results.Add(new DiscoveryResult(
                listing.TenantId,
                listing.BranchId,
                listing.RestaurantName,
                listing.Description,
                listing.Cuisines,
                listing.DietaryOptions,
                listing.BranchName,
                listing.Address,
                listing.Latitude,
                listing.Longitude,
                distance is { } d ? Math.Round(d, 2) : null,
                isOpen,
                listing.OpeningHours));
        }

        var ordered = query.HasLocation
            ? results.OrderBy(r => r.DistanceKm)
            : results.OrderByDescending(r => r.IsOpenNow).ThenBy(r => r.RestaurantName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.BranchName, StringComparer.OrdinalIgnoreCase);

        return ordered.Take(Math.Clamp(query.Limit, 1, DiscoveryQuery.MaxLimit)).ToList();
    }
}
