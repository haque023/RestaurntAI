using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RestaurantAI.Identity.Api.Security;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Integration;

public sealed record CreatedRestaurant(Guid Id, string Name);

public sealed record QrVenue(Guid TenantId, Guid BranchId, Guid TableId, string RestaurantName, string BranchName, string TableLabel);

public sealed record ListedBranch(Guid BranchId, string BranchName);

public sealed record ListedRestaurant(Guid TenantId, string RestaurantName, IReadOnlyList<ListedBranch> Branches);

/// <summary>
/// Calls other platform services through the gateway. Onboarding uses a short-lived service
/// token (Identity is the token issuer); venue lookups use the public discovery endpoints.
/// </summary>
public sealed class PlatformServicesClient(HttpClient httpClient, PlatformTokenIssuer tokens)
{
    /// <summary>Fixed subject for tokens Identity issues to itself.</summary>
    public static readonly Guid ServiceUserId = new("00000000-0000-7000-8000-00000000a001");

    public async Task<CreatedRestaurant> CreateRestaurantAsync(
        string name,
        string currency,
        string timeZone,
        bool selfService,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/restaurants/")
        {
            Content = JsonContent.Create(new { name, defaultCurrency = currency, timeZone, selfService }),
        };
        var token = tokens.Issue(new TokenGrant(ServiceUserId, null, null, null, [PlatformRoles.PlatformAdministrator], TimeSpan.FromMinutes(2)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PlatformServiceException((int)response.StatusCode, body);
        }

        return await response.Content.ReadFromJsonAsync<CreatedRestaurant>(cancellationToken)
            ?? throw new PlatformServiceException(502, "The restaurant service returned no body.");
    }

    public Task<QrVenue?> ResolveQrAsync(string qrToken, CancellationToken cancellationToken) =>
        GetOrNullAsync<QrVenue>($"/api/discovery/restaurants/qr/{Uri.EscapeDataString(qrToken)}", cancellationToken);

    public Task<ListedRestaurant?> GetListedRestaurantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        GetOrNullAsync<ListedRestaurant>($"/api/discovery/restaurants/tenants/{tenantId}", cancellationToken);

    private async Task<T?> GetOrNullAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}

public sealed class PlatformServiceException(int statusCode, string detail)
    : Exception($"Platform service call failed ({statusCode}): {detail}")
{
    public int StatusCode { get; } = statusCode;
}
