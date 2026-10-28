using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Reservation.Api.Persistence;
using RestaurantAI.Reservation.Application.Reservations;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Results;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Reservation.Api.Endpoints;

public static class ReservationEndpoints
{
    private static readonly Error TableAlreadyBooked =
        Error.Conflict("reservation.table_unavailable", "The table is already booked for an overlapping time.");

    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var reservations = app.MapGroup("/api/reservations").WithTags("Reservations");

        reservations.MapPost("/", CreateAsync).RequirePermission(PlatformPermissions.ReservationsCreate);
        reservations.MapGet("/{reservationId:guid}", GetAsync).RequirePermission(PlatformPermissions.ReservationsRead);
        reservations.MapPost("/{reservationId:guid}/cancel", CancelAsync).RequirePermission(PlatformPermissions.ReservationsCancel);
        reservations.MapGet("/", ListAsync).RequirePermission(PlatformPermissions.ReservationsManage);
        reservations.MapGet("/mine", ListMineAsync).RequirePermission(PlatformPermissions.ReservationsRead);
        reservations.MapPost("/{reservationId:guid}/seat", (Guid reservationId, ITenantContext tenant, ReservationDbContext db, CancellationToken ct) =>
            ChangeAsync(reservationId, tenant, db, ct, r => r.Seat())).RequirePermission(PlatformPermissions.ReservationsManage);
        reservations.MapPost("/{reservationId:guid}/complete", (Guid reservationId, ITenantContext tenant, ReservationDbContext db, CancellationToken ct) =>
            ChangeAsync(reservationId, tenant, db, ct, r => r.Complete())).RequirePermission(PlatformPermissions.ReservationsManage);
        reservations.MapPost("/{reservationId:guid}/no-show", (Guid reservationId, ITenantContext tenant, ReservationDbContext db, CancellationToken ct) =>
            ChangeAsync(reservationId, tenant, db, ct, r => r.MarkNoShow())).RequirePermission(PlatformPermissions.ReservationsManage);

        return app;
    }

    /// <summary>A branch's reservations for a day (local calendar date in UTC bounds supplied by the client).</summary>
    private static async Task<IResult> ListAsync(
        Guid? branchId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        ITenantContext tenant,
        ReservationDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
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

    private static async Task<IResult> ListMineAsync(ITenantContext tenant, ReservationDbContext dbContext, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Reservations.AsNoTracking()
            .Where(r => r.CustomerId == tenant.UserId)
            .OrderByDescending(r => r.StartsAt).Take(50)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(ReservationResponse.From).ToList());
    }

    private static async Task<IResult> ChangeAsync(
        Guid reservationId,
        ITenantContext tenant,
        ReservationDbContext dbContext,
        CancellationToken cancellationToken,
        Action<Domain.Reservation> change)
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

    private static Task<IResult> CreateAsync(
        CreateReservationCommand command,
        [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey,
        ReservationService service,
        IdempotencyStore<ReservationDbContext> idempotency,
        ReservationDbContext dbContext,
        ITenantContext tenant,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(tenant.TenantId ?? Guid.Empty, idempotencyKey, "reservations.create", IdempotencyStore<ReservationDbContext>.ComputeRequestHash(command)),
            ct => Task.FromResult(service.Create(command).Map(ReservationResponse.From)),
            StatusCodes.Status201Created,
            response => TypedResults.Created($"/api/reservations/{response.Id}", response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken,
            onDbConflict: ex => ex.IsExclusionViolation() ? TableAlreadyBooked.ToProblem() : null);

    private static async Task<IResult> GetAsync(Guid reservationId, ReservationService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(reservationId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(ReservationResponse.From(result.Value)) : result.Error.ToProblem();
    }

    private static Task<IResult> CancelAsync(
        Guid reservationId,
        CancelReservationCommand command,
        [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey,
        ReservationService service,
        IdempotencyStore<ReservationDbContext> idempotency,
        ReservationDbContext dbContext,
        ITenantContext tenant,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        idempotency.ExecuteAsync(
            dbContext,
            new IdempotentRequest(
                tenant.TenantId ?? Guid.Empty,
                idempotencyKey,
                "reservations.cancel",
                IdempotencyStore<ReservationDbContext>.ComputeRequestHash(new { reservationId, command }),
                KeyRequired: false),
            async ct => (await service.CancelAsync(reservationId, command, ct)).Map(ReservationResponse.From),
            StatusCodes.Status200OK,
            response => TypedResults.Ok(response),
            error => error.ToProblem(),
            httpContext,
            cancellationToken);
}
