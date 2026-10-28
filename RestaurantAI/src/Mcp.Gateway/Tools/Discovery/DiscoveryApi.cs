using System.Globalization;
using System.Text.Json;
using RestaurantAI.Mcp.Gateway.Clients;

namespace RestaurantAI.Mcp.Gateway.Tools.Discovery;

/// <summary>Restaurant listing as returned by the public discovery API (fields the tools use).</summary>
public sealed record ListingDto(
    Guid TenantId,
    Guid BranchId,
    string RestaurantName,
    string Description,
    string[] Cuisines,
    string[] DietaryOptions,
    string BranchName,
    string Address,
    double? DistanceKm,
    bool IsOpenNow);

public sealed record DishDto(Guid MenuItemId, Guid TenantId, Guid BranchId, string Name, string Category, string Description, string[] DietaryTags, decimal Price, string Currency);

/// <summary>Thin wrapper over the public discovery endpoints. Results only ever contain listed restaurants.</summary>
public sealed class DiscoveryApi(BusinessApiClient api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ListingDto>> SearchRestaurantsAsync(
        string? query,
        string[]? cuisines,
        string[]? diets,
        double? latitude,
        double? longitude,
        double? radiusKm,
        bool openNow,
        IReadOnlyCollection<Guid>? branchIds,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string> { $"limit={limit}" };
        Add(parameters, "q", query);
        Add(parameters, "cuisines", cuisines is { Length: > 0 } ? string.Join(',', cuisines) : null);
        Add(parameters, "diets", diets is { Length: > 0 } ? string.Join(',', diets) : null);
        if (latitude is { } lat && longitude is { } lng)
        {
            parameters.Add($"lat={lat.ToString(CultureInfo.InvariantCulture)}");
            parameters.Add($"lng={lng.ToString(CultureInfo.InvariantCulture)}");
            parameters.Add($"radiusKm={(radiusKm ?? 10).ToString(CultureInfo.InvariantCulture)}");
        }

        if (openNow)
        {
            parameters.Add("openNow=true");
        }

        if (branchIds is { Count: > 0 })
        {
            parameters.Add($"branchIds={string.Join(',', branchIds.Take(100))}");
        }

        var body = await api.GetAsync("/api/discovery/restaurants/?" + string.Join('&', parameters), cancellationToken);
        return JsonSerializer.Deserialize<List<ListingDto>>(body, Json) ?? [];
    }

    public async Task<IReadOnlyList<DishDto>> SearchDishesAsync(
        string? query,
        string[]? diets,
        decimal? maxPrice,
        IReadOnlyCollection<Guid>? branchIds,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string> { $"limit={limit}" };
        Add(parameters, "q", query);
        Add(parameters, "diets", diets is { Length: > 0 } ? string.Join(',', diets) : null);
        if (maxPrice is { } price)
        {
            parameters.Add($"maxPrice={price.ToString(CultureInfo.InvariantCulture)}");
        }

        if (branchIds is { Count: > 0 })
        {
            parameters.Add($"branchIds={string.Join(',', branchIds.Take(200))}");
        }

        var body = await api.GetAsync("/api/discovery/menu-items?" + string.Join('&', parameters), cancellationToken);
        return JsonSerializer.Deserialize<List<DishDto>>(body, Json) ?? [];
    }

    /// <summary>The consumer-app path that opens a restaurant's menu; the assistant links to it.</summary>
    public static string MenuLink(Guid tenantId, Guid branchId) => $"/r/{tenantId}/{branchId}";

    private static void Add(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}
