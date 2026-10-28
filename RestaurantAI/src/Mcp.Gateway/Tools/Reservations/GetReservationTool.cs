using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;

namespace RestaurantAI.Mcp.Gateway.Tools.Reservations;

[McpServerToolType]
public sealed class GetReservationTool(McpAuthorizationService authorization, McpAuditService audit, BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.GetReservation, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Returns the live status and details of a reservation.")]
    public Task<string> ExecuteAsync([Description("Reservation id.")] Guid reservationId, CancellationToken cancellationToken)
    {
        var caller = authorization.Authorize(McpToolNames.GetReservation);
        return audit.TrackAsync(McpToolNames.GetReservation, caller, () => api.GetAsync($"/api/reservations/{reservationId}", cancellationToken));
    }
}
