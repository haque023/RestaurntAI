using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Controllers;

/// <summary>Guests (or the AI waiter on their behalf) ask for a person; floor staff handle the queue.</summary>
[ApiController]
[Route("api/service-requests")]
[Tags("Service requests")]
public sealed class ServiceRequestsController(IServiceRequestService service) : ControllerBase
{
    [HttpPost]
    [RequirePermission(PlatformPermissions.ServiceRequest)]
    public Task<IResult> OpenAsync(OpenServiceRequest request, CancellationToken cancellationToken) =>
        service.OpenAsync(request, cancellationToken);

    [HttpGet("mine")]
    [RequirePermission(PlatformPermissions.ServiceRequest)]
    public Task<IResult> ListMineAsync(CancellationToken cancellationToken) =>
        service.ListMineAsync(cancellationToken);

    [HttpGet]
    [RequirePermission(PlatformPermissions.OrdersManage)]
    public Task<IResult> ListAsync(Guid? branchId, bool? open, CancellationToken cancellationToken) =>
        service.ListAsync(branchId, open, cancellationToken);

    [HttpPost("{requestId:guid}/acknowledge")]
    [RequirePermission(PlatformPermissions.OrdersManage)]
    public Task<IResult> AcknowledgeAsync(Guid requestId, CancellationToken cancellationToken) =>
        service.AcknowledgeAsync(requestId, cancellationToken);

    [HttpPost("{requestId:guid}/resolve")]
    [RequirePermission(PlatformPermissions.OrdersManage)]
    public Task<IResult> ResolveAsync(Guid requestId, CancellationToken cancellationToken) =>
        service.ResolveAsync(requestId, cancellationToken);
}
