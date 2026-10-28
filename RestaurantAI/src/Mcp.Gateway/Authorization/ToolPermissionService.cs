using RestaurantAI.Mcp.Gateway.Context;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Mcp.Gateway.Authorization;

/// <summary>Declares what each MCP tool requires. Unknown tools are denied.</summary>
public sealed record ToolDefinition(string Name, string RequiredPermission, bool Mutates, bool RequiresBranch);

public sealed record ToolAuthorizationResult(bool Allowed, string? Reason = null)
{
    public static readonly ToolAuthorizationResult Allow = new(true);

    public static ToolAuthorizationResult Deny(string reason) => new(false, reason);
}

/// <summary>Stable MCP tool identifiers exposed to AI agents.</summary>
public static class McpToolNames
{
    public const string GetMenu = "get_menu";
    public const string SearchMenu = "search_menu";
    public const string GetAvailability = "get_menu_item_availability";
    public const string CreateOrder = "create_order";
    public const string GetOrder = "get_order_status";
    public const string CancelOrder = "cancel_order";
    public const string CreateReservation = "create_reservation";
    public const string GetReservation = "get_reservation";
    public const string CancelReservation = "cancel_reservation";
}

public sealed class ToolPermissionService
{
    private static readonly Dictionary<string, ToolDefinition> Definitions = new ToolDefinition[]
    {
        new(McpToolNames.GetMenu, PlatformPermissions.MenuRead, Mutates: false, RequiresBranch: true),
        new(McpToolNames.SearchMenu, PlatformPermissions.MenuRead, Mutates: false, RequiresBranch: true),
        new(McpToolNames.GetAvailability, PlatformPermissions.MenuRead, Mutates: false, RequiresBranch: false),
        new(McpToolNames.CreateOrder, PlatformPermissions.OrdersCreate, Mutates: true, RequiresBranch: true),
        new(McpToolNames.GetOrder, PlatformPermissions.OrdersRead, Mutates: false, RequiresBranch: false),
        new(McpToolNames.CancelOrder, PlatformPermissions.OrdersCancel, Mutates: true, RequiresBranch: false),
        new(McpToolNames.CreateReservation, PlatformPermissions.ReservationsCreate, Mutates: true, RequiresBranch: true),
        new(McpToolNames.GetReservation, PlatformPermissions.ReservationsRead, Mutates: false, RequiresBranch: false),
        new(McpToolNames.CancelReservation, PlatformPermissions.ReservationsCancel, Mutates: true, RequiresBranch: false),
    }.ToDictionary(d => d.Name, StringComparer.Ordinal);

    public ToolDefinition? Find(string toolName) => Definitions.GetValueOrDefault(toolName);

    public ToolAuthorizationResult Check(string toolName, ToolCallerContext caller, Guid? requestedBranchId)
    {
        if (Find(toolName) is not { } definition)
        {
            return ToolAuthorizationResult.Deny($"Tool '{toolName}' is not registered.");
        }

        if (!caller.User.HasPermission(definition.RequiredPermission))
        {
            return ToolAuthorizationResult.Deny($"Missing permission '{definition.RequiredPermission}'.");
        }

        if (definition.RequiresBranch && requestedBranchId is null)
        {
            return ToolAuthorizationResult.Deny("A branch is required for this tool.");
        }

        if (requestedBranchId is { } branchId && !caller.Branch.Allows(branchId) && !caller.User.IsPlatformAdministrator)
        {
            return ToolAuthorizationResult.Deny("The caller is not allowed to act on this branch.");
        }

        return ToolAuthorizationResult.Allow;
    }
}
