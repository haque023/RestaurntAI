using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Tags("Orders")]
public sealed class OrdersController(IOrderService service) : ControllerBase
{
    [HttpPost]
    [RequirePermission(PlatformPermissions.OrdersCreate)]
    public Task<IResult> PlaceOrderAsync(PlaceOrderCommand command, [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey, CancellationToken cancellationToken) =>
        service.PlaceOrderAsync(command, idempotencyKey, HttpContext, cancellationToken);

    [HttpGet("{orderId:guid}")]
    [RequirePermission(PlatformPermissions.OrdersRead)]
    public Task<IResult> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        service.GetOrderAsync(orderId, cancellationToken);

    [HttpPost("{orderId:guid}/cancel")]
    [RequirePermission(PlatformPermissions.OrdersCancel)]
    public Task<IResult> CancelOrderAsync(Guid orderId, CancelOrderCommand command, [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey, CancellationToken cancellationToken) =>
        service.CancelOrderAsync(orderId, command, idempotencyKey, HttpContext, cancellationToken);

    [HttpPost("{orderId:guid}/status")]
    [RequirePermission(PlatformPermissions.OrdersManage)]
    public Task<IResult> ChangeStatusAsync(Guid orderId, ChangeOrderStatusCommand command, CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(orderId, command, cancellationToken);

    [HttpGet]
    [RequirePermission(PlatformPermissions.OrdersManage)]
    public Task<IResult> ListAsync(Guid? branchId, string? status, DateTimeOffset? since, int? take, CancellationToken cancellationToken) =>
        service.ListAsync(branchId, status, since, take, cancellationToken);

    [HttpGet("mine")]
    [RequirePermission(PlatformPermissions.OrdersRead)]
    public Task<IResult> ListMineAsync(CancellationToken cancellationToken) =>
        service.ListMineAsync(cancellationToken);
}
