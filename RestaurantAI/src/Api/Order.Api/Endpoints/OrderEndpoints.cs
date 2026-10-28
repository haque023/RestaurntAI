using Microsoft.AspNetCore.Mvc;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Order.Api.Persistence;
using RestaurantAI.Order.Application.Orders;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders");

        orders.MapPost("/", PlaceOrderAsync).RequirePermission(PlatformPermissions.OrdersCreate);
        orders.MapGet("/{orderId:guid}", GetOrderAsync).RequirePermission(PlatformPermissions.OrdersRead);
        orders.MapPost("/{orderId:guid}/cancel", CancelOrderAsync).RequirePermission(PlatformPermissions.OrdersCancel);

        return app;
    }

    private static Task<IResult> PlaceOrderAsync(
        PlaceOrderCommand command,
        [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey,
        OrderService service,
        IdempotencyStore<OrderDbContext> idempotency,
        OrderDbContext dbContext,
        ITenantContext tenant,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(tenant.TenantId ?? Guid.Empty, idempotencyKey, "orders.place", IdempotencyStore<OrderDbContext>.ComputeRequestHash(command)),
            async ct => (await service.PlaceAsync(command, ct)).Map(OrderResponse.From),
            StatusCodes.Status201Created,
            response => TypedResults.Created($"/api/orders/{response.Id}", response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken);

    private static async Task<IResult> GetOrderAsync(Guid orderId, OrderService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(orderId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(OrderResponse.From(result.Value)) : result.Error.ToProblem();
    }

    private static Task<IResult> CancelOrderAsync(
        Guid orderId,
        CancelOrderCommand command,
        [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey,
        OrderService service,
        IdempotencyStore<OrderDbContext> idempotency,
        OrderDbContext dbContext,
        ITenantContext tenant,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(
                tenant.TenantId ?? Guid.Empty,
                idempotencyKey,
                "orders.cancel",
                IdempotencyStore<OrderDbContext>.ComputeRequestHash(new { orderId, command }),
                KeyRequired: false),
            async ct => (await service.CancelAsync(orderId, command, ct)).Map(OrderResponse.From),
            StatusCodes.Status200OK,
            response => TypedResults.Ok(response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken);
}
