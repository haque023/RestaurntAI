namespace RestaurantAI.Order.Api.Models;

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
