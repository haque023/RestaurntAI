using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;

namespace RestaurantAI.Mcp.Gateway.Tools.Discovery;

[McpServerToolType]
public sealed class SearchRestaurantsTool(McpAuthorizationService authorization, McpAuditService audit, DiscoveryApi discovery)
{
    [McpServerTool(Name = McpToolNames.SearchRestaurants, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Finds restaurants listed on the platform by name/keywords, cuisine, dietary options, distance from a location and opening hours. Returns each restaurant with distance, open-now status and a menu link.")]
    public Task<string> ExecuteAsync(
        [Description("Restaurant name or keywords, e.g. 'biryani' or 'Spice Route'. Optional.")] string? query = null,
        [Description("Cuisines that must all match, lower-case, e.g. ['bangladeshi','indian'].")] string[]? cuisines = null,
        [Description("Dietary options that must all match, e.g. ['vegan','halal'].")] string[]? dietaryOptions = null,
        [Description("Latitude to search around. Use the customer's location when it is known.")] double? latitude = null,
        [Description("Longitude to search around.")] double? longitude = null,
        [Description("Search radius in km (default 10, max 100). Only used with a location.")] double? radiusKm = null,
        [Description("Only restaurants open right now.")] bool openNow = false,
        CancellationToken cancellationToken = default)
    {
        var caller = authorization.Authorize(McpToolNames.SearchRestaurants);
        return audit.TrackAsync(McpToolNames.SearchRestaurants, caller, async () =>
        {
            var listings = await discovery.SearchRestaurantsAsync(query, cuisines, dietaryOptions, latitude, longitude, radiusKm, openNow, null, 15, cancellationToken);
            return JsonSerializer.Serialize(listings.Select(l => new
            {
                restaurant = l.RestaurantName,
                branch = l.BranchName,
                l.Address,
                cuisines = l.Cuisines,
                dietaryOptions = l.DietaryOptions,
                distanceKm = l.DistanceKm is { } d ? Math.Round(d, 1) : (double?)null,
                openNow = l.IsOpenNow,
                description = l.Description.Length > 200 ? l.Description[..200] + "…" : l.Description,
                menuLink = DiscoveryApi.MenuLink(l.TenantId, l.BranchId),
            }));
        });
    }
}
