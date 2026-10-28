using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;

namespace RestaurantAI.Mcp.Gateway.Tools.Discovery;

[McpServerToolType]
public sealed class SearchDishesTool(McpAuthorizationService authorization, McpAuditService audit, DiscoveryApi discovery)
{
    [McpServerTool(Name = McpToolNames.SearchDishes, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Finds dishes that are available now across listed restaurants, optionally near a location, under a price and matching dietary tags. Prices are live from each restaurant's menu.")]
    public Task<string> ExecuteAsync(
        [Description("Dish name or keywords, e.g. 'kacchi', 'spicy chicken', 'pasta'. Optional if dietary tags are given.")] string? query = null,
        [Description("Dietary tags that must all match, e.g. ['vegan'].")] string[]? dietaryTags = null,
        [Description("Maximum price in the restaurant's currency.")] decimal? maxPrice = null,
        [Description("Latitude to search around. Use the customer's location when it is known.")] double? latitude = null,
        [Description("Longitude to search around.")] double? longitude = null,
        [Description("Search radius in km (default 10, max 100). Only used with a location.")] double? radiusKm = null,
        [Description("Only restaurants open right now.")] bool openNow = false,
        CancellationToken cancellationToken = default)
    {
        var caller = authorization.Authorize(McpToolNames.SearchDishes);
        return audit.TrackAsync(McpToolNames.SearchDishes, caller, async () =>
        {
            if (string.IsNullOrWhiteSpace(query) && dietaryTags is not { Length: > 0 })
            {
                return "Provide a dish name or at least one dietary tag.";
            }

            // With a location, narrow the dish search to nearby listed branches first.
            IReadOnlyList<ListingDto>? nearby = null;
            if (latitude is not null && longitude is not null)
            {
                nearby = await discovery.SearchRestaurantsAsync(null, null, null, latitude, longitude, radiusKm, openNow, null, 100, cancellationToken);
                if (nearby.Count == 0)
                {
                    return "[]";
                }
            }

            var dishes = await discovery.SearchDishesAsync(query, dietaryTags, maxPrice, nearby?.Select(n => n.BranchId).ToList(), 40, cancellationToken);
            if (dishes.Count == 0)
            {
                return "[]";
            }

            // Attach restaurant names, distance and opening state; drops dishes of unlisted restaurants.
            var listings = nearby ?? await discovery.SearchRestaurantsAsync(
                null, null, null, null, null, null, openNow, dishes.Select(d => d.BranchId).Distinct().ToList(), 100, cancellationToken);
            var byBranch = listings.ToDictionary(l => l.BranchId);

            var results = dishes
                .Where(d => byBranch.ContainsKey(d.BranchId))
                .Select(d => (Dish: d, Listing: byBranch[d.BranchId]))
                .OrderBy(x => x.Listing.DistanceKm ?? double.MaxValue)
                .ThenBy(x => x.Dish.Price)
                .Take(15)
                .Select(x => new
                {
                    dish = x.Dish.Name,
                    x.Dish.Category,
                    description = x.Dish.Description.Length > 160 ? x.Dish.Description[..160] + "…" : x.Dish.Description,
                    dietaryTags = x.Dish.DietaryTags,
                    price = x.Dish.Price,
                    currency = x.Dish.Currency,
                    restaurant = x.Listing.RestaurantName,
                    branch = x.Listing.BranchName,
                    distanceKm = x.Listing.DistanceKm is { } d ? Math.Round(d, 1) : (double?)null,
                    openNow = x.Listing.IsOpenNow,
                    menuLink = DiscoveryApi.MenuLink(x.Listing.TenantId, x.Listing.BranchId),
                });

            return JsonSerializer.Serialize(results);
        });
    }
}
