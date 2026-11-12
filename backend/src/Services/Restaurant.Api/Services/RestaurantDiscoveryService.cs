using Microsoft.EntityFrameworkCore;

namespace RestaurantAI.Restaurant.Api.Services;

/// <summary>
/// Public, anonymous restaurant discovery for consumers. Reads only the discovery read model,
/// which contains listed (Active) restaurants and public branch data.
/// </summary>
public sealed class RestaurantDiscoveryService(RestaurantDbContext dbContext, TimeProvider timeProvider) : IRestaurantDiscoveryService
{
    /// <summary>Candidates fetched from the database before exact distance / opening-hours filtering.</summary>
    private const int CandidateLimit = 500;

    public async Task<IResult> SearchAsync(string? q, string? cuisines, string? diets, double? lat, double? lng, double? radiusKm, bool? openNow, string? branchIds, string? tenantIds, int? limit, CancellationToken cancellationToken)
    {
        if (lat.HasValue != lng.HasValue || lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            return TypedResults.Problem(title: "discovery.invalid_location", detail: "Provide valid lat and lng together.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryParseGuids(branchIds, out var branches) || !TryParseGuids(tenantIds, out var tenants))
        {
            return TypedResults.Problem(title: "discovery.invalid_ids", detail: "branchIds and tenantIds must be comma-separated GUIDs (max 100).", statusCode: StatusCodes.Status400BadRequest);
        }

        var query = new DiscoveryQuery
        {
            Text = q?.Trim(),
            Cuisines = SplitTags(cuisines),
            Diets = SplitTags(diets),
            Latitude = lat,
            Longitude = lng,
            RadiusKm = Math.Clamp(radiusKm ?? 10, 0.1, DiscoveryQuery.MaxRadiusKm),
            OpenNow = openNow ?? false,
            BranchIds = branches,
            TenantIds = tenants,
            Limit = limit ?? 30,
        };

        var candidates = dbContext.DiscoveryListings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var pattern = $"%{EscapeLike(query.Text.ToLowerInvariant())}%";
            candidates = candidates.Where(l => EF.Functions.Like(l.SearchText, pattern));
        }

        foreach (var cuisine in query.Cuisines)
        {
            candidates = candidates.Where(l => l.Cuisines.Contains(cuisine));
        }

        foreach (var diet in query.Diets)
        {
            candidates = candidates.Where(l => l.DietaryOptions.Contains(diet));
        }

        if (query.BranchIds.Count > 0)
        {
            candidates = candidates.Where(l => query.BranchIds.Contains(l.BranchId));
        }

        if (query.TenantIds.Count > 0)
        {
            candidates = candidates.Where(l => query.TenantIds.Contains(l.TenantId));
        }

        if (query.HasLocation)
        {
            var (minLat, maxLat, minLon, maxLon) = GeoDistance.BoundingBox(query.Latitude!.Value, query.Longitude!.Value, query.RadiusKm);
            candidates = candidates.Where(l => l.Latitude >= minLat && l.Latitude <= maxLat && l.Longitude >= minLon && l.Longitude <= maxLon);
        }

        var rows = await candidates.OrderBy(l => l.RestaurantName).Take(CandidateLimit).ToListAsync(cancellationToken);
        var listings = rows.Select(r => new DiscoveryListing(
            r.TenantId, r.BranchId, r.RestaurantName, r.Description, r.Cuisines, r.DietaryOptions,
            r.BranchName, r.Address, r.Latitude, r.Longitude, r.TimeZone, r.OpeningHours));

        return TypedResults.Ok(DiscoveryRanker.Apply(listings, query, timeProvider.GetUtcNow()));
    }

    /// <summary>A listed restaurant and its listed branches; 404 when the restaurant is not listed.</summary>
    public async Task<IResult> GetListedRestaurantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.DiscoveryListings.AsNoTracking().Where(l => l.TenantId == tenantId).OrderBy(l => l.BranchName).ToListAsync(cancellationToken);
        return rows.Count == 0
            ? TypedResults.NotFound()
            : TypedResults.Ok(new ListedRestaurantDto(tenantId, rows[0].RestaurantName, rows.Select(r => new ListedBranchDto(r.BranchId, r.BranchName, r.Address)).ToList()));
    }

    /// <summary>Resolves a printed table QR code; only works while the restaurant is listed.</summary>
    public async Task<IResult> ResolveQrAsync(string token, CancellationToken cancellationToken)
    {
        if (token.Length is < 8 or > 64)
        {
            return TypedResults.NotFound();
        }

        var venue = await (
            from qr in dbContext.QrCodes.AsNoTracking()
            join listing in dbContext.DiscoveryListings.AsNoTracking() on qr.BranchId equals listing.BranchId
            where qr.Token == token
            select new QrVenueDto(qr.TenantId, qr.BranchId, qr.TableId, listing.RestaurantName, listing.BranchName, qr.TableLabel))
            .SingleOrDefaultAsync(cancellationToken);

        return venue is null ? TypedResults.NotFound() : TypedResults.Ok(venue);
    }

    public async Task<IResult> CuisinesAsync(CancellationToken cancellationToken)
    {
        var all = await dbContext.DiscoveryListings.AsNoTracking().Select(l => l.Cuisines).ToListAsync(cancellationToken);
        return TypedResults.Ok(all.SelectMany(c => c).Distinct().Order(StringComparer.Ordinal).ToList());
    }

    private static List<string> SplitTags(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .Take(10)
            .ToList();

    private static bool TryParseGuids(string? value, out List<Guid> ids)
    {
        ids = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id) || ids.Count >= 100)
            {
                return false;
            }

            ids.Add(id);
        }

        return true;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
