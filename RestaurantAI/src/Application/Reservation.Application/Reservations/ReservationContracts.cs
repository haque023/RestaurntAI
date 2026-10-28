namespace RestaurantAI.Reservation.Application.Reservations;

public sealed record CreateReservationCommand(
    Guid BranchId,
    Guid? TableId,
    int PartySize,
    DateTimeOffset StartsAt,
    int? DurationMinutes,
    string? SpecialRequests);

public sealed record CancelReservationCommand(string Reason);

public sealed record ReservationResponse(
    Guid Id,
    Guid BranchId,
    Guid? TableId,
    int PartySize,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status,
    string? SpecialRequests)
{
    public static ReservationResponse From(Domain.Reservation r) =>
        new(r.Id, r.BranchId, r.TableId, r.PartySize, r.StartsAt, r.EndsAt, r.Status.ToString(), r.SpecialRequests);
}

public interface IReservationRepository
{
    void Add(Domain.Reservation reservation);

    Task<Domain.Reservation?> GetAsync(Guid id, CancellationToken cancellationToken);
}
