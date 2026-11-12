using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Controllers;

/// <summary>Tables and their QR codes. Managers configure tables; floor staff update table status.</summary>
[ApiController]
[Route("api/restaurants/current")]
[Tags("Tables")]
public sealed class TablesController(ITableService service) : ControllerBase
{
    [HttpGet("branches/{branchId:guid}/tables")]
    [RequirePermission(PlatformPermissions.OrdersRead)]
    public Task<IResult> ListAsync(Guid branchId, CancellationToken cancellationToken) =>
        service.ListAsync(branchId, cancellationToken);

    [HttpPost("branches/{branchId:guid}/tables")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> CreateAsync(Guid branchId, TableRequest request, CancellationToken cancellationToken) =>
        service.CreateAsync(branchId, request, cancellationToken);

    [HttpPut("tables/{tableId:guid}")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> UpdateAsync(Guid tableId, TableRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(tableId, request, cancellationToken);

    [HttpDelete("tables/{tableId:guid}")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> DeleteAsync(Guid tableId, CancellationToken cancellationToken) =>
        service.DeleteAsync(tableId, cancellationToken);

    [HttpPost("tables/{tableId:guid}/rotate-qr")]
    [RequirePermission(PlatformPermissions.RestaurantManage)]
    public Task<IResult> RotateQrAsync(Guid tableId, CancellationToken cancellationToken) =>
        service.RotateQrAsync(tableId, cancellationToken);

    [HttpPut("tables/{tableId:guid}/status")]
    [RequirePermission(PlatformPermissions.OrdersManage)]
    public Task<IResult> SetStatusAsync(Guid tableId, TableStatusRequest request, CancellationToken cancellationToken) =>
        service.SetStatusAsync(tableId, request, cancellationToken);
}
