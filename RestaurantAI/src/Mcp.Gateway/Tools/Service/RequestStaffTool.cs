using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;

namespace RestaurantAI.Mcp.Gateway.Tools.Service;

[McpServerToolType]
public sealed class RequestStaffTool(McpAuthorizationService authorization, McpAuditService audit, BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.RequestStaff, ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Asks a member of staff to come to the guest: when the guest asks for a waiter or the bill, or when the request is outside what the assistant can do (complaints, allergies needing confirmation, payment problems, anything unusual).")]
    public Task<string> ExecuteAsync(
        [Description("CallWaiter, RequestBill, Assistance or AiEscalation (use AiEscalation when you cannot help).")] string kind,
        [Description("Short note for staff, e.g. 'Guest has a severe nut allergy and wants to confirm the curry is safe'.")] string note,
        [Description("Branch id. Optional when the session is bound to a branch.")] Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var caller = authorization.Authorize(McpToolNames.RequestStaff, branchId);
        var body = new
        {
            kind = Enum.TryParse<ServiceKind>(kind, ignoreCase: true, out var parsed) ? parsed.ToString() : nameof(ServiceKind.AiEscalation),
            note,
            branchId = branchId ?? caller.Branch.BranchId,
            tableId = caller.Branch.TableId,
        };

        return audit.TrackAsync(McpToolNames.RequestStaff, caller, () => api.PostAsync("/api/service-requests/", body, null, cancellationToken));
    }

    private enum ServiceKind
    {
        CallWaiter,
        RequestBill,
        Assistance,
        AiEscalation,
    }
}
