using Microsoft.EntityFrameworkCore;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Services;

public sealed class OrderService(OrderWorkflow workflow, IdempotencyStore<OrderDbContext> idempotency, OrderDbContext dbContext, ITenantContext tenant) : IOrderService
{
    public Task<IResult> PlaceOrderAsync(PlaceOrderCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(tenant.TenantId ?? Guid.Empty, idempotencyKey, "orders.place", IdempotencyStore<OrderDbContext>.ComputeRequestHash(command)),
            async ct => (await workflow.PlaceAsync(command, ct)).Map(OrderResponse.From),
            StatusCodes.Status201Created,
            response => TypedResults.Created($"/api/orders/{response.Id}", response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken);

    public async Task<IResult> ChangeStatusAsync(Guid orderId, ChangeOrderStatusCommand command, CancellationToken cancellationToken)
    {
        var result = await workflow.ChangeStatusAsync(orderId, command, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(OrderResponse.From(result.Value));
    }

    /// <summary>The kitchen/floor board: a branch's orders, newest first.</summary>
    public async Task<IResult> ListAsync(Guid? branchId, string? status, DateTimeOffset? since, int? take, CancellationToken cancellationToken)
    {
        if (tenant.BranchId is { } own && branchId is { } requested && requested != own)
        {
            return OrderWorkflow.BranchForbidden.ToProblem();
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
    public async Task<IResult> ListMineAsync(CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders.AsNoTracking()
            .Where(o => o.CustomerId == tenant.UserId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(orders.Select(OrderResponse.From).ToList());
    }

    public async Task<IResult> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await workflow.GetAsync(orderId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(OrderResponse.From(result.Value)) : result.Error.ToProblem();
    }

    public Task<IResult> CancelOrderAsync(Guid orderId, CancelOrderCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(
                tenant.TenantId ?? Guid.Empty,
                idempotencyKey,
                "orders.cancel",
                IdempotencyStore<OrderDbContext>.ComputeRequestHash(new { orderId, command }),
                KeyRequired: false),
            async ct => (await workflow.CancelAsync(orderId, command, ct)).Map(OrderResponse.From),
            StatusCodes.Status200OK,
            response => TypedResults.Ok(response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken);
}
