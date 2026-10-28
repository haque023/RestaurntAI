using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAI.Order.Domain;
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
        orders.MapPost("/{orderId:guid}/status", ChangeStatusAsync).RequirePermission(PlatformPermissions.OrdersManage);
        orders.MapGet("/", ListAsync).RequirePermission(PlatformPermissions.OrdersManage);
        orders.MapGet("/mine", ListMineAsync).RequirePermission(PlatformPermissions.OrdersRead);
        orders.MapGet("/stats", OrderStatsEndpoint.GetAsync).RequirePermission(PlatformPermissions.AnalyticsRead);

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

    private static async Task<IResult> ChangeStatusAsync(
        Guid orderId,
        ChangeOrderStatusCommand command,
        OrderService service,
        OrderDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await service.ChangeStatusAsync(orderId, command, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(OrderResponse.From(result.Value));
    }

    /// <summary>The kitchen/floor board: a branch's orders, newest first.</summary>
    private static async Task<IResult> ListAsync(
        Guid? branchId,
        string? status,
        DateTimeOffset? since,
        int? take,
        ITenantContext tenant,
        OrderDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (tenant.BranchId is { } own && branchId is { } requested && requested != own)
        {
            return OrderService.BranchForbidden.ToProblem();
        }

        var query = dbContext.Orders.AsNoTracking();
        if ((branchId ?? tenant.BranchId) is { } branch)
        {
            query = query.Where(o => o.BranchId == branch);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = new List<OrderStatus>();
            foreach (var part in status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<OrderStatus>(part, ignoreCase: true, out var parsed))
                {
                    return TypedResults.Problem(title: "order.invalid_status", detail: $"Unknown status '{part}'.", statusCode: StatusCodes.Status400BadRequest);
                }

                statuses.Add(parsed);
            }

            query = query.Where(o => statuses.Contains(o.Status));
        }

        if (since is { } from)
        {
            query = query.Where(o => o.PlacedAt >= from);
        }

        var orders = await query.OrderByDescending(o => o.PlacedAt).Take(Math.Clamp(take ?? 100, 1, 500)).ToListAsync(cancellationToken);
        return TypedResults.Ok(orders.Select(OrderResponse.From).ToList());
    }

    /// <summary>The signed-in customer's orders at the current restaurant.</summary>
    private static async Task<IResult> ListMineAsync(ITenantContext tenant, OrderDbContext dbContext, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders.AsNoTracking()
            .Where(o => o.CustomerId == tenant.UserId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(orders.Select(OrderResponse.From).ToList());
    }

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
