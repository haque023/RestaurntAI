using System.Net.Http.Json;
using ModelContextProtocol;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Mcp.Gateway.Clients;

/// <summary>
/// Calls the business APIs through the YARP gateway with the end user's token, so the
/// business platform remains the source of truth and applies its own authorization.
/// </summary>
public sealed class BusinessApiClient(HttpClient httpClient)
{
    public Task<string> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), cancellationToken);

    public Task<string> PostAsync<TBody>(string path, TBody body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation(PlatformHeaders.IdempotencyKey, idempotencyKey);
        }

        return SendAsync(request, cancellationToken);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await httpClient.SendAsync(request, cancellationToken))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Surface the business error to the agent without internal details.
                throw new McpException($"The restaurant system rejected the request ({(int)response.StatusCode}): {Truncate(body)}");
            }

            return body;
        }
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
