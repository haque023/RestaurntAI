using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Reservation.Api.Models;

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Seated,
    Completed,
    Cancelled,
    NoShow,
}

/// <summary>
/// A table reservation. Overlapping reservations for the same table are rejected by a
/// PostgreSQL exclusion constraint, so conflicts are prevented transactionally.
/// </summary>
public sealed class Reservation : AggregateRoot<Guid>, IBranchScoped
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(90);

    private Reservation()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid? TableId { get; private set; }

    public Guid CustomerId { get; private set; }

    public int PartySize { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public ReservationStatus Status { get; private set; }

    public string? SpecialRequests { get; private set; }

    public string? CancellationReason { get; private set; }

    public static Reservation Create(
        Guid tenantId,
        Guid branchId,
        Guid? tableId,
        Guid customerId,
        int partySize,
        DateTimeOffset startsAt,
        TimeSpan? duration,
        string? specialRequests,
        DateTimeOffset now)
    {
        if (partySize is < 1 or > 50)
        {
            throw new DomainException("reservation.invalid_party_size", "Party size must be between 1 and 50.");
        }

        if (startsAt <= now)
        {
            throw new DomainException("reservation.in_past", "Reservations must be made for a future time.");
        }

        var reservation = new Reservation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BranchId = branchId,
            TableId = tableId,
            CustomerId = customerId,
            PartySize = partySize,
            StartsAt = startsAt,
            EndsAt = startsAt.Add(duration ?? DefaultDuration),
            Status = ReservationStatus.Confirmed,
            SpecialRequests = specialRequests,
        };
        reservation.Raise(new ReservationCreated(reservation.Id, tenantId, branchId, customerId, partySize, startsAt));
        return reservation;
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        if (Status is ReservationStatus.Cancelled or ReservationStatus.Completed or ReservationStatus.NoShow)
        {
            throw new DomainException("reservation.not_cancellable", $"A {Status} reservation cannot be cancelled.");
        }

        if (now >= StartsAt)
        {
            throw new DomainException("reservation.already_started", "A reservation cannot be cancelled after it has started.");
        }

        Status = ReservationStatus.Cancelled;
        CancellationReason = reason;
        Raise(new ReservationCancelled(Id, TenantId, BranchId, reason));
    }

    public void Seat()
    {
        if (Status is not (ReservationStatus.Confirmed or ReservationStatus.Pending))
        {
            throw new DomainException("reservation.invalid_transition", $"A {Status} reservation cannot be seated.");
        }

        Status = ReservationStatus.Seated;
        Raise(new ReservationSeated(Id, TenantId, BranchId));
    }

    public void Complete()
    {
        if (Status != ReservationStatus.Seated)
        {
            throw new DomainException("reservation.invalid_transition", "Only seated reservations can be completed.");
        }

        Status = ReservationStatus.Completed;
    }

    public void MarkNoShow()
    {
        if (Status != ReservationStatus.Confirmed)
        {
            throw new DomainException("reservation.invalid_transition", "Only confirmed reservations can be marked as no-show.");
        }

        Status = ReservationStatus.NoShow;
        Raise(new ReservationNoShow(Id, TenantId, BranchId, CustomerId));
    }
}

public sealed record ReservationCreated(Guid ReservationId, Guid TenantId, Guid BranchId, Guid CustomerId, int PartySize, DateTimeOffset StartsAt) : DomainEvent;

public sealed record ReservationCancelled(Guid ReservationId, Guid TenantId, Guid BranchId, string Reason) : DomainEvent;

public sealed record ReservationSeated(Guid ReservationId, Guid TenantId, Guid BranchId) : DomainEvent;

public sealed record ReservationNoShow(Guid ReservationId, Guid TenantId, Guid BranchId, Guid CustomerId) : DomainEvent;
