using Microsoft.AspNetCore.Mvc;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Reservation.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Tags("Reservations")]
public sealed class ReservationsController(IReservationService service) : ControllerBase
{
    [HttpPost]
    [RequirePermission(PlatformPermissions.ReservationsCreate)]
    public Task<IResult> CreateAsync(CreateReservationCommand command, [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey, CancellationToken cancellationToken) =>
        service.CreateAsync(command, idempotencyKey, HttpContext, cancellationToken);

    [HttpGet("{reservationId:guid}")]
    [RequirePermission(PlatformPermissions.ReservationsRead)]
    public Task<IResult> GetAsync(Guid reservationId, CancellationToken cancellationToken) =>
        service.GetAsync(reservationId, cancellationToken);

    [HttpPost("{reservationId:guid}/cancel")]
    [RequirePermission(PlatformPermissions.ReservationsCancel)]
    public Task<IResult> CancelAsync(Guid reservationId, CancelReservationCommand command, [FromHeader(Name = PlatformHeaders.IdempotencyKey)] string? idempotencyKey, CancellationToken cancellationToken) =>
        service.CancelAsync(reservationId, command, idempotencyKey, HttpContext, cancellationToken);

    [HttpGet]
    [RequirePermission(PlatformPermissions.ReservationsManage)]
    public Task<IResult> ListAsync(Guid? branchId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) =>
        service.ListAsync(branchId, from, to, cancellationToken);

    [HttpGet("mine")]
    [RequirePermission(PlatformPermissions.ReservationsRead)]
    public Task<IResult> ListMineAsync(CancellationToken cancellationToken) =>
        service.ListMineAsync(cancellationToken);

    [HttpPost("{reservationId:guid}/seat")]
    [RequirePermission(PlatformPermissions.ReservationsManage)]
    public Task<IResult> SeatAsync(Guid reservationId, CancellationToken cancellationToken) =>
        service.SeatAsync(reservationId, cancellationToken);

    [HttpPost("{reservationId:guid}/complete")]
    [RequirePermission(PlatformPermissions.ReservationsManage)]
    public Task<IResult> CompleteAsync(Guid reservationId, CancellationToken cancellationToken) =>
        service.CompleteAsync(reservationId, cancellationToken);

    [HttpPost("{reservationId:guid}/no-show")]
    [RequirePermission(PlatformPermissions.ReservationsManage)]
    public Task<IResult> NoShowAsync(Guid reservationId, CancellationToken cancellationToken) =>
        service.NoShowAsync(reservationId, cancellationToken);
}
