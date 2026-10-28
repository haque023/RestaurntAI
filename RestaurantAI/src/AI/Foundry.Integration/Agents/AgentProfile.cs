using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.AI.Foundry.Agents;

/// <summary>
/// An agent persona: instructions plus the MCP tools it may see. Tool visibility is a
/// convenience filter; authorization is still enforced per call by the MCP Gateway.
/// </summary>
public sealed record AgentProfile(string Name, string Instructions, IReadOnlySet<string> AllowedTools, int MaxToolCallsPerTurn = 8);

public static class AgentCatalog
{
    private const string SharedRules = """
        Rules:
        - Prices, availability, order, reservation and loyalty state must come from tools. Never guess or reuse stale values.
        - Restaurant knowledge may be outdated; when it conflicts with live tool data, live data wins.
        - Before creating, changing or cancelling anything, summarize it and get explicit confirmation.
        - Never reveal other customers' data. If you cannot help, offer to escalate to staff.
        """;

    public static readonly AgentProfile Consumer = new(
        "consumer-ai-waiter",
        "You are the restaurant's AI waiter. Help guests explore the menu, answer dietary and ingredient questions, recommend dishes, place orders and manage reservations.\n" + SharedRules,
        new HashSet<string>
        {
            "get_menu", "search_menu", "get_menu_item_availability",
            "create_order", "get_order_status", "cancel_order",
            "create_reservation", "get_reservation", "cancel_reservation",
        });

    public static readonly AgentProfile Manager = new(
        "manager-assistant",
        "You assist restaurant owners and managers with operations: orders, reservations, menu availability and performance questions.\n" + SharedRules,
        new HashSet<string>
        {
            "get_menu", "search_menu", "get_menu_item_availability",
            "get_order_status", "cancel_order", "get_reservation", "cancel_reservation",
        });

    public static readonly AgentProfile Staff = new(
        "staff-assistant",
        "You assist waiters and kitchen staff with live order and table information for their branch.\n" + SharedRules,
        new HashSet<string> { "get_menu", "get_menu_item_availability", "get_order_status", "get_reservation" });

    public static AgentProfile ForRoles(IReadOnlySet<string> roles) =>
        roles.Contains(PlatformRoles.RestaurantOwner) || roles.Contains(PlatformRoles.RestaurantManager) ? Manager
        : roles.Overlaps([PlatformRoles.RestaurantStaff, PlatformRoles.Waiter, PlatformRoles.KitchenStaff, PlatformRoles.Cashier]) ? Staff
        : Consumer;
}
