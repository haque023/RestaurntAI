namespace RestaurantAI.Reservation.Api.IServices;

public interface IReservationRepository
{
    void Add(Models.Reservation reservation);

    Task<Models.Reservation?> GetAsync(Guid id, CancellationToken cancellationToken);
}
