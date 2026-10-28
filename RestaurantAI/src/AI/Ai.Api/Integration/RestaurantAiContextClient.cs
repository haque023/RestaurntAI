using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace RestaurantAI.AI.Api.Integration;

public sealed record OpeningPeriodDto(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

public sealed record BranchDto(Guid Id, string Name, string Address, double? Latitude, double? Longitude, IReadOnlyList<OpeningPeriodDto> OpeningHours);

/// <summary>Governance, entitlements and knowledge for one restaurant, from the restaurant service.</summary>
public sealed record RestaurantAiContext(
    Guid TenantId,
    string RestaurantName,
    string Status,
    bool AiEnabled,
    int AiTurnsPerMonth,
    string Knowledge,
    string Tone,
    string DefaultCurrency,
    string TimeZone,
    string? Phone,
    string? Website,
    IReadOnlyList<BranchDto> Branches);

/// <summary>
/// Reads the restaurant's AI context with the caller's own token (so tenant isolation applies)
/// and caches it briefly per tenant; settings changes reach the AI within a minute.
/// </summary>
public sealed class RestaurantAiContextClient(HttpClient httpClient, IMemoryCache cache)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<RestaurantAiContext?> GetAsync(Guid tenantId, string accessToken, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key(tenantId), out RestaurantAiContext? cached))
        {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/restaurants/current/ai-context");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var context = await response.Content.ReadFromJsonAsync<RestaurantAiContext>(Json, cancellationToken);
        if (context is not null && context.TenantId == tenantId)
        {
            cache.Set(Key(tenantId), context, CacheFor);
        }

        return context;
    }

    private static string Key(Guid tenantId) => $"ai-context:{tenantId:N}";
}
