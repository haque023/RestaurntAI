namespace RestaurantAI.Reservation.Api.IServices;

public interface IReservationService
{
    Task<IResult> CreateAsync(CreateReservationCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken);

    Task<IResult> GetAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<IResult> CancelAsync(Guid reservationId, CancelReservationCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken);

    Task<IResult> ListAsync(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);

    Task<IResult> ListMineAsync(CancellationToken cancellationToken);

    Task<IResult> SeatAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<IResult> CompleteAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<IResult> NoShowAsync(Guid reservationId, CancellationToken cancellationToken);
}
