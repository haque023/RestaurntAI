using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Gateway.Api.Tests;

public sealed class GatewayRoutingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Liveness_endpoint_is_anonymous_and_returns_correlation_id()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(PlatformHeaders.CorrelationId));
    }

    [Theory]
    [InlineData("/api/orders/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/menu/branches/00000000-0000-0000-0000-000000000001/items")]
    [InlineData("/api/admin/overview")]
    public async Task Protected_routes_reject_anonymous_callers_at_the_gateway(string path)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
