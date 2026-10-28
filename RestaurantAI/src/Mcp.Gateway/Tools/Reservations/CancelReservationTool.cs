using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;
using RestaurantAI.Mcp.Gateway.Idempotency;

namespace RestaurantAI.Mcp.Gateway.Tools.Reservations;

[McpServerToolType]
public sealed class CancelReservationTool(
    McpAuthorizationService authorization,
    McpAuditService audit,
    IdempotencyService idempotency,
    BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.CancelReservation, ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Cancels an upcoming reservation. Requires explicit customer confirmation.")]
    public Task<string> ExecuteAsync(
        [Description("Reservation id.")] Guid reservationId,
        [Description("Reason given by the customer.")] string reason,
        CancellationToken cancellationToken)
    {
        var caller = authorization.Authorize(McpToolNames.CancelReservation);
        var body = new { reason };

        return audit.TrackAsync(McpToolNames.CancelReservation, caller, () =>
            idempotency.ExecuteAsync(
                McpToolNames.CancelReservation,
                caller,
                new { reservationId, reason },
                explicitKey: null,
                (key, ct) => api.PostAsync($"/api/reservations/{reservationId}/cancel", body, key, ct),
                cancellationToken));
    }
}
