using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RestaurantAI.Order.Api.IServices;
using RestaurantAI.Order.Api.Models;
using RestaurantAI.Order.Api.Services;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Integration.Tests;

public sealed class OrderApiTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static readonly string[] CustomerPermissions =
        [PlatformPermissions.OrdersCreate, PlatformPermissions.OrdersRead, PlatformPermissions.OrdersCancel];

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private (Guid Tenant, Guid Branch, Guid Item) Seed()
    {
        Assert.SkipWhen(factory.StartupError is not null, factory.StartupError ?? string.Empty);

        var item = Guid.NewGuid();
        factory.Catalog.Add(new CatalogItem(item, "Spicy Ramen", 13.90m, "USD", IsAvailable: true));
        return (Guid.NewGuid(), Guid.NewGuid(), item);
    }

    private static object OrderBody(Guid branch, Guid item, int quantity = 2) => new
    {
        branchId = branch,
        channel = "AiWaiter",
        items = new[] { new { menuItemId = item, quantity } },
    };

    [Fact]
    public async Task Retried_order_with_same_idempotency_key_is_created_once()
    {
        var (tenant, branch, item) = Seed();
        var client = factory.CreateClientFor(tenant, Guid.NewGuid(), CustomerPermissions);

        var first = await PostOrder(client, OrderBody(branch, item), "retry-key-1");
        var second = await PostOrder(client, OrderBody(branch, item), "retry-key-1");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));

        var a = await first.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        var b = await second.Content.ReadFromJsonAsync<OrderResponse>(Ct);
        Assert.Equal(a!.Id, b!.Id);
        Assert.Equal(27.80m, a.Total);

        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM orders.orders WHERE tenant_id = @t", tenant));
        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM orders.outbox_messages WHERE tenant_id = @t AND type = 'order.OrderCreated'", tenant));
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_is_rejected()
    {
        var (tenant, branch, item) = Seed();
        var client = factory.CreateClientFor(tenant, Guid.NewGuid(), CustomerPermissions);

        await PostOrder(client, OrderBody(branch, item, quantity: 1), "reuse-key");
        var conflicting = await PostOrder(client, OrderBody(branch, item, quantity: 3), "reuse-key");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflicting.StatusCode);
    }

    [Fact]
    public async Task Orders_require_an_idempotency_key()
    {
        var (tenant, branch, item) = Seed();
        var client = factory.CreateClientFor(tenant, Guid.NewGuid(), CustomerPermissions);

        var response = await PostOrder(client, OrderBody(branch, item), idempotencyKey: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Another_tenant_cannot_read_the_order()
    {
        var (tenant, branch, item) = Seed();
        var user = Guid.NewGuid();
        var owner = factory.CreateClientFor(tenant, user, CustomerPermissions);
        var created = await (await PostOrder(owner, OrderBody(branch, item), "tenant-key")).Content.ReadFromJsonAsync<OrderResponse>(Ct);

        // Same user id, different tenant: blocked by the query filter and by PostgreSQL RLS.
        var intruder = factory.CreateClientFor(Guid.NewGuid(), user, CustomerPermissions);
        var response = await intruder.GetAsync($"/api/orders/{created!.Id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/orders/{created.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Row_level_security_hides_rows_without_a_tenant_session()
    {
        var (tenant, branch, item) = Seed();
        var client = factory.CreateClientFor(tenant, Guid.NewGuid(), CustomerPermissions);
        await PostOrder(client, OrderBody(branch, item), "rls-key");

        var appConnection = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Platform");
        await using var connection = new NpgsqlConnection(appConnection);
        await connection.OpenAsync(Ct);

        await using var command = new NpgsqlCommand("SELECT count(*) FROM orders.orders", connection);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(Ct))!);
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        var (tenant, branch, item) = Seed();
        var client = factory.CreateClientFor(tenant, Guid.NewGuid(), PlatformPermissions.OrdersRead);

        var response = await PostOrder(client, OrderBody(branch, item), "perm-key");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostOrder(HttpClient client, object body, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add(PlatformHeaders.IdempotencyKey, idempotencyKey);
        }

        return client.SendAsync(request, Ct);
    }

    private async Task<long> CountAsync(string sql, Guid tenant)
    {
        // Superuser connection bypasses RLS to verify what was actually persisted.
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("t", tenant);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
