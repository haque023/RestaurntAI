using Microsoft.EntityFrameworkCore;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Services;

/// <summary>Guests (or the AI waiter on their behalf) ask for a person; floor staff handle the queue.</summary>
public sealed class ServiceRequestService(ITenantContext tenant, OrderDbContext dbContext, TimeProvider timeProvider) : IServiceRequestService
{
    public async Task<IResult> OpenAsync(OpenServiceRequest request, CancellationToken cancellationToken)
    {
        // The branch and table bound to the session (QR) win over anything in the body; only staff pick a table.
        var tableId = tenant.TableId ?? (tenant.HasPermission(PlatformPermissions.OrdersManage) ? request.TableId : null);
        var branchId = tenant.BranchId ?? request.BranchId;
        if (branchId is null)
        {
            return TypedResults.Problem(title: "branch.required", detail: "Choose the branch you are at.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (tenant.BranchId is { } own && request.BranchId is { } asked && asked != own)
        {
            return OrderWorkflow.BranchForbidden.ToProblem();
        }

        // One open request per guest and kind is enough; repeated taps do not spam staff.
        var existing = await dbContext.ServiceRequests.FirstOrDefaultAsync(
            r => r.CustomerId == tenant.UserId && r.BranchId == branchId && r.Kind == request.Kind && r.Status != ServiceRequestStatus.Resolved,
            cancellationToken);
        if (existing is not null)
        {
            return TypedResults.Ok(ServiceRequestResponse.From(existing));
        }

        var serviceRequest = ServiceRequest.Open(
            tenant.TenantId!.Value, branchId.Value, tableId, tenant.UserId!.Value, request.Kind, request.Note, timeProvider.GetUtcNow());
        dbContext.ServiceRequests.Add(serviceRequest);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/service-requests/{serviceRequest.Id}", ServiceRequestResponse.From(serviceRequest));
    }

    public async Task<IResult> ListMineAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.ServiceRequests.AsNoTracking()
            .Where(r => r.CustomerId == tenant.UserId)
            .OrderByDescending(r => r.CreatedAt).Take(20)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(ServiceRequestResponse.From).ToList());
    }

    public async Task<IResult> ListAsync(Guid? branchId, bool? open, CancellationToken cancellationToken)
    {
        if (tenant.BranchId is { } own && branchId is { } requested && requested != own)
        {
            return OrderWorkflow.BranchForbidden.ToProblem();
        }

        var query = dbContext.ServiceRequests.AsNoTracking();
        if ((branchId ?? tenant.BranchId) is { } branch)
        {
            query = query.Where(r => r.BranchId == branch);
        }

        if (open ?? true)
        {
            query = query.Where(r => r.Status != ServiceRequestStatus.Resolved);
        }

        var rows = await query.OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(ServiceRequestResponse.From).ToList());
    }

    public Task<IResult> AcknowledgeAsync(Guid requestId, CancellationToken cancellationToken) =>
        HandleAsync(requestId, cancellationToken, r => r.Acknowledge(tenant.UserId!.Value));

    public Task<IResult> ResolveAsync(Guid requestId, CancellationToken cancellationToken) =>
        HandleAsync(requestId, cancellationToken, r => r.Resolve(tenant.UserId!.Value, timeProvider.GetUtcNow()));

    private async Task<IResult> HandleAsync(Guid requestId, CancellationToken cancellationToken, Action<ServiceRequest> change)
    {
        var request = await dbContext.ServiceRequests.SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null || (tenant.BranchId is { } own && request.BranchId != own))
        {
            return TypedResults.NotFound();
        }

        change(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ServiceRequestResponse.From(request));
    }
}
