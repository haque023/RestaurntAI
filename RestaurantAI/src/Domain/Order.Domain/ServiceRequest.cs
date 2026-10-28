using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Order.Domain;

public enum ServiceRequestKind
{
    CallWaiter,
    RequestBill,
    Assistance,

    /// <summary>The AI waiter could not help and handed over to a person.</summary>
    AiEscalation,
}

public enum ServiceRequestStatus
{
    Open,
    Acknowledged,
    Resolved,
}

/// <summary>A guest asking for a person: call waiter, bill, help, or an AI hand-over to staff.</summary>
public sealed class ServiceRequest : AggregateRoot<Guid>, IBranchScoped
{
    public const int MaxNoteLength = 500;

    private ServiceRequest()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid? TableId { get; private set; }

    public Guid CustomerId { get; private set; }

    public ServiceRequestKind Kind { get; private set; }

    public string Note { get; private set; } = string.Empty;

    public ServiceRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? HandledBy { get; private set; }

    public static ServiceRequest Open(Guid tenantId, Guid branchId, Guid? tableId, Guid customerId, ServiceRequestKind kind, string? note, DateTimeOffset now)
    {
        var text = (note ?? string.Empty).Trim();
        var request = new ServiceRequest
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BranchId = branchId,
            TableId = tableId,
            CustomerId = customerId,
            Kind = kind,
            Note = text.Length > MaxNoteLength ? text[..MaxNoteLength] : text,
            Status = ServiceRequestStatus.Open,
            CreatedAt = now,
        };
        request.Raise(new ServiceRequestOpened(request.Id, tenantId, branchId, tableId, kind));
        return request;
    }

    public void Acknowledge(Guid staffUserId)
    {
        if (Status == ServiceRequestStatus.Open)
        {
            Status = ServiceRequestStatus.Acknowledged;
            HandledBy = staffUserId;
        }
    }

    public void Resolve(Guid staffUserId, DateTimeOffset now)
    {
        if (Status != ServiceRequestStatus.Resolved)
        {
            Status = ServiceRequestStatus.Resolved;
            HandledBy = staffUserId;
            ResolvedAt = now;
        }
    }
}

public sealed record ServiceRequestOpened(Guid RequestId, Guid TenantId, Guid BranchId, Guid? TableId, ServiceRequestKind Kind) : DomainEvent;
