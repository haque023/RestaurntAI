using Microsoft.EntityFrameworkCore;
using RestaurantAI.Order.Api.Persistence;
using RestaurantAI.Order.Application.Orders;
using RestaurantAI.Order.Domain;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Endpoints;

/// <summary>Guests (or the AI waiter on their behalf) ask for a person; floor staff handle the queue.</summary>
public static class ServiceRequestEndpoints
{
    public sealed record OpenServiceRequest(ServiceRequestKind Kind, string? Note, Guid? BranchId, Guid? TableId);

    public sealed record ServiceRequestResponse(
        Guid Id,
        Guid BranchId,
        Guid? TableId,
        string Kind,
        string Note,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ResolvedAt)
    {
        public static ServiceRequestResponse From(ServiceRequest r) =>
            new(r.Id, r.BranchId, r.TableId, r.Kind.ToString(), r.Note, r.Status.ToString(), r.CreatedAt, r.ResolvedAt);
    }

    public static IEndpointRouteBuilder MapServiceRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var requests = app.MapGroup("/api/service-requests").WithTags("Service requests");

        requests.MapPost("/", OpenAsync).RequirePermission(PlatformPermissions.ServiceRequest);
        requests.MapGet("/mine", ListMineAsync).RequirePermission(PlatformPermissions.ServiceRequest);
        requests.MapGet("/", ListAsync).RequirePermission(PlatformPermissions.OrdersManage);
        requests.MapPost("/{requestId:guid}/acknowledge", (Guid requestId, ITenantContext tenant, OrderDbContext db, TimeProvider time, CancellationToken ct) =>
            HandleAsync(requestId, tenant, db, ct, r => r.Acknowledge(tenant.UserId!.Value))).RequirePermission(PlatformPermissions.OrdersManage);
        requests.MapPost("/{requestId:guid}/resolve", (Guid requestId, ITenantContext tenant, OrderDbContext db, TimeProvider time, CancellationToken ct) =>
            HandleAsync(requestId, tenant, db, ct, r => r.Resolve(tenant.UserId!.Value, time.GetUtcNow()))).RequirePermission(PlatformPermissions.OrdersManage);

        return app;
    }

    private static async Task<IResult> OpenAsync(
        OpenServiceRequest request,
        ITenantContext tenant,
        OrderDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
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
            return OrderService.BranchForbidden.ToProblem();
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

    private static async Task<IResult> ListMineAsync(ITenantContext tenant, OrderDbContext dbContext, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ServiceRequests.AsNoTracking()
            .Where(r => r.CustomerId == tenant.UserId)
            .OrderByDescending(r => r.CreatedAt).Take(20)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(ServiceRequestResponse.From).ToList());
    }

    private static async Task<IResult> ListAsync(Guid? branchId, bool? open, ITenantContext tenant, OrderDbContext dbContext, CancellationToken cancellationToken)
    {
        if (tenant.BranchId is { } own && branchId is { } requested && requested != own)
        {
            return OrderService.BranchForbidden.ToProblem();
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

    private static async Task<IResult> HandleAsync(Guid requestId, ITenantContext tenant, OrderDbContext dbContext, CancellationToken cancellationToken, Action<ServiceRequest> change)
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
