using Microsoft.AspNetCore.Mvc;
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

        return app;
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
