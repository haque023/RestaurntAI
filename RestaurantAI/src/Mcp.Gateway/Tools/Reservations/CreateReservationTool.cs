using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;
using RestaurantAI.Mcp.Gateway.Idempotency;

namespace RestaurantAI.Mcp.Gateway.Tools.Reservations;

[McpServerToolType]
public sealed class CreateReservationTool(
    McpAuthorizationService authorization,
    McpAuditService audit,
    IdempotencyService idempotency,
    BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.CreateReservation, ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Creates a table reservation after the customer confirmed date, time and party size. Conflicts are rejected by the restaurant system.")]
    public Task<string> ExecuteAsync(
        [Description("Number of guests (1-50).")] int partySize,
        [Description("Start time in ISO 8601 with offset, e.g. 2026-10-20T19:30:00+02:00.")] DateTimeOffset startsAt,
        [Description("Duration in minutes (default 90).")] int? durationMinutes = null,
        [Description("Special requests, e.g. high chair, window seat.")] string? specialRequests = null,
        [Description("Branch id. Optional when the session is bound to a branch.")] Guid? branchId = null,
        [Description("Specific table id, if the customer chose one.")] Guid? tableId = null,
        [Description("Optional client-generated idempotency key for this confirmation.")] string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var caller = authorization.Authorize(McpToolNames.CreateReservation, branchId);
        var body = new
        {
            branchId = branchId ?? caller.Branch.BranchId!.Value,
            tableId,
            partySize,
            startsAt,
            durationMinutes,
            specialRequests,
        };

        return audit.TrackAsync(McpToolNames.CreateReservation, caller, () =>
            idempotency.ExecuteAsync(
                McpToolNames.CreateReservation,
                caller,
                body,
                idempotencyKey,
                (key, ct) => api.PostAsync("/api/reservations", body, key, ct),
                cancellationToken));
    }
}
