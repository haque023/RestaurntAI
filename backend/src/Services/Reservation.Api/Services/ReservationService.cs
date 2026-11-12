using Microsoft.EntityFrameworkCore;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Results;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Reservation.Api.Services;

public sealed class ReservationService(ITenantContext tenant, ReservationDbContext dbContext, TimeProvider timeProvider, ReservationWorkflow workflow, IdempotencyStore<ReservationDbContext> idempotency) : IReservationService
{
    private static readonly Error TableAlreadyBooked =
        Error.Conflict("reservation.table_unavailable", "The table is already booked for an overlapping time.");

    /// <summary>A branch's reservations for a day (local calendar date in UTC bounds supplied by the client).</summary>
    public async Task<IResult> ListAsync(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        if (tenant.BranchId is { } own && branchId is { } requested && requested != own)
        {
            return Error.Forbidden("branch.forbidden", "You do not have access to this branch.").ToProblem();
        }

        var start = from ?? new DateTimeOffset(timeProvider.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var end = to ?? start.AddDays(1);
        var query = dbContext.Reservations.AsNoTracking().Where(r => r.StartsAt >= start && r.StartsAt < end);
        if ((branchId ?? tenant.BranchId) is { } branch)
        {
            query = query.Where(r => r.BranchId == branch);
        }

        var rows = await query.OrderBy(r => r.StartsAt).Take(500).ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(ReservationResponse.From).ToList());
    }

    public async Task<IResult> ListMineAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Reservations.AsNoTracking()
            .Where(r => r.CustomerId == tenant.UserId)
            .OrderByDescending(r => r.StartsAt).Take(50)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(ReservationResponse.From).ToList());
    }

    public Task<IResult> SeatAsync(Guid reservationId, CancellationToken cancellationToken) =>
        ChangeAsync(reservationId, cancellationToken, r => r.Seat());

    public Task<IResult> CompleteAsync(Guid reservationId, CancellationToken cancellationToken) =>
        ChangeAsync(reservationId, cancellationToken, r => r.Complete());

    public Task<IResult> NoShowAsync(Guid reservationId, CancellationToken cancellationToken) =>
        ChangeAsync(reservationId, cancellationToken, r => r.MarkNoShow());

    private async Task<IResult> ChangeAsync(Guid reservationId, CancellationToken cancellationToken, Action<Models.Reservation> change)
    {
        var reservation = await dbContext.Reservations.SingleOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation is null || (tenant.BranchId is { } own && reservation.BranchId != own))
        {
            return TypedResults.NotFound();
        }

        change(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ReservationResponse.From(reservation));
    }

    public Task<IResult> CreateAsync(CreateReservationCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(tenant.TenantId ?? Guid.Empty, idempotencyKey, "reservations.create", IdempotencyStore<ReservationDbContext>.ComputeRequestHash(command)),
            ct => Task.FromResult(workflow.Create(command).Map(ReservationResponse.From)),
            StatusCodes.Status201Created,
            response => TypedResults.Created($"/api/reservations/{response.Id}", response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken,
            onDbConflict: ex => ex.IsExclusionViolation() ? TableAlreadyBooked.ToProblem() : null);

    public async Task<IResult> GetAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var result = await workflow.GetAsync(reservationId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(ReservationResponse.From(result.Value)) : result.Error.ToProblem();
    }

    public Task<IResult> CancelAsync(Guid reservationId, CancelReservationCommand command, string? idempotencyKey, HttpContext httpContext, CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(
                tenant.TenantId ?? Guid.Empty,
                idempotencyKey,
                "reservations.cancel",
                IdempotencyStore<ReservationDbContext>.ComputeRequestHash(new { reservationId, command }),
                KeyRequired: false),
            async ct => (await workflow.CancelAsync(reservationId, command, ct)).Map(ReservationResponse.From),
            StatusCodes.Status200OK,
            response => TypedResults.Ok(response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken);
}
