using RestaurantAI.SharedKernel.Results;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Reservation.Application.Reservations;

public sealed class ReservationService(IReservationRepository reservations, ITenantContext tenant, TimeProvider timeProvider)
{
    public static readonly Error NotFound = Error.NotFound("reservation.not_found", "Reservation not found.");

    public Result<Domain.Reservation> Create(CreateReservationCommand command)
    {
        if (tenant.TenantId is not { } tenantId || tenant.UserId is not { } userId)
        {
            return Error.Forbidden("tenant.required", "A tenant context is required.");
        }

        if (command.DurationMinutes is < 15 or > 480)
        {
            return Error.Validation("reservation.invalid_duration", "Duration must be between 15 and 480 minutes.");
        }

        var reservation = Domain.Reservation.Create(
            tenantId,
            command.BranchId,
            command.TableId,
            userId,
            command.PartySize,
            command.StartsAt,
            command.DurationMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            command.SpecialRequests,
            timeProvider.GetUtcNow());

        reservations.Add(reservation);
        return reservation;
    }

    public async Task<Result<Domain.Reservation>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var reservation = await reservations.GetAsync(id, cancellationToken);
        return reservation is not null && CanSee(reservation) ? reservation : NotFound;
    }

    // Customers see their own reservations; staff with reservations.manage see their branch.
    private bool CanSee(Domain.Reservation reservation) =>
        reservation.CustomerId == tenant.UserId
        || (tenant.HasPermission(PlatformPermissions.ReservationsManage)
            && (tenant.BranchId is null || tenant.BranchId == reservation.BranchId));

    public async Task<Result<Domain.Reservation>> CancelAsync(Guid id, CancelReservationCommand command, CancellationToken cancellationToken)
    {
        var found = await GetAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found;
        }

        found.Value.Cancel(command.Reason, timeProvider.GetUtcNow());
        return found;
    }
}
