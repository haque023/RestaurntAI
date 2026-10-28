namespace RestaurantAI.SharedKernel.Tenancy;

/// <summary>
/// Fine-grained permissions carried in the <c>permission</c> claim. Every transactional API
/// and MCP tool checks User + Tenant + Branch + Role + Permission.
/// </summary>
public static class PlatformPermissions
{
    public const string MenuRead = "menu.read";
    public const string MenuWrite = "menu.write";

    public const string OrdersCreate = "orders.create";
    public const string OrdersRead = "orders.read";
    public const string OrdersCancel = "orders.cancel";
    public const string OrdersManage = "orders.manage";

    public const string ReservationsCreate = "reservations.create";
    public const string ReservationsRead = "reservations.read";
    public const string ReservationsCancel = "reservations.cancel";
    public const string ReservationsManage = "reservations.manage";

    public const string LoyaltyRead = "loyalty.read";
    public const string LoyaltyManage = "loyalty.manage";

    public const string RestaurantManage = "restaurant.manage";
    public const string ConsumerProfile = "consumer.profile";

    public const string StaffManage = "staff.manage";
    public const string AnalyticsRead = "analytics.read";
    public const string AiManage = "ai.manage";

    /// <summary>Ask staff for help (call waiter, AI escalation).</summary>
    public const string ServiceRequest = "service.request";

    /// <summary>Cross-restaurant discovery through the AI assistant.</summary>
    public const string DiscoverySearch = "discovery.search";
}
