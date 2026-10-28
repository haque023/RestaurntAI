namespace RestaurantAI.SharedKernel.Tenancy;

/// <summary>
/// The permissions each role grants. Identity issues tokens from this map, so clients can never
/// choose their own permissions. Platform administrators pass every check and need no entries.
/// </summary>
public static class PlatformRolePermissions
{
    private static readonly string[] CustomerPermissions =
    [
        PlatformPermissions.MenuRead,
        PlatformPermissions.OrdersCreate,
        PlatformPermissions.OrdersRead,
        PlatformPermissions.OrdersCancel,
        PlatformPermissions.ReservationsCreate,
        PlatformPermissions.ReservationsRead,
        PlatformPermissions.ReservationsCancel,
        PlatformPermissions.LoyaltyRead,
        PlatformPermissions.ConsumerProfile,
        PlatformPermissions.ServiceRequest,
        PlatformPermissions.DiscoverySearch,
    ];

    private static readonly string[] OwnerPermissions =
    [
        PlatformPermissions.RestaurantManage,
        PlatformPermissions.StaffManage,
        PlatformPermissions.AnalyticsRead,
        PlatformPermissions.AiManage,
        PlatformPermissions.MenuRead,
        PlatformPermissions.MenuWrite,
        PlatformPermissions.OrdersCreate,
        PlatformPermissions.OrdersRead,
        PlatformPermissions.OrdersCancel,
        PlatformPermissions.OrdersManage,
        PlatformPermissions.ReservationsCreate,
        PlatformPermissions.ReservationsRead,
        PlatformPermissions.ReservationsCancel,
        PlatformPermissions.ReservationsManage,
        PlatformPermissions.LoyaltyRead,
        PlatformPermissions.LoyaltyManage,
        PlatformPermissions.ServiceRequest,
    ];

    private static readonly Dictionary<string, string[]> ByRole = new(StringComparer.Ordinal)
    {
        [PlatformRoles.Customer] = CustomerPermissions,
        [PlatformRoles.RestaurantOwner] = OwnerPermissions,
        [PlatformRoles.RestaurantManager] = OwnerPermissions,
        [PlatformRoles.Waiter] =
        [
            PlatformPermissions.MenuRead,
            PlatformPermissions.OrdersCreate,
            PlatformPermissions.OrdersRead,
            PlatformPermissions.OrdersManage,
            PlatformPermissions.ReservationsRead,
            PlatformPermissions.ReservationsManage,
            PlatformPermissions.ServiceRequest,
        ],
        [PlatformRoles.KitchenStaff] =
        [
            PlatformPermissions.MenuRead,
            PlatformPermissions.OrdersRead,
            PlatformPermissions.OrdersManage,
        ],
        [PlatformRoles.Cashier] =
        [
            PlatformPermissions.MenuRead,
            PlatformPermissions.OrdersCreate,
            PlatformPermissions.OrdersRead,
            PlatformPermissions.OrdersCancel,
            PlatformPermissions.OrdersManage,
        ],
        [PlatformRoles.RestaurantStaff] =
        [
            PlatformPermissions.MenuRead,
            PlatformPermissions.OrdersRead,
            PlatformPermissions.OrdersManage,
            PlatformPermissions.ReservationsRead,
            PlatformPermissions.ServiceRequest,
        ],
        [PlatformRoles.MarketingUser] =
        [
            PlatformPermissions.MenuRead,
            PlatformPermissions.AnalyticsRead,
            PlatformPermissions.LoyaltyRead,
        ],
        [PlatformRoles.PlatformAdministrator] = [],
    };

    /// <summary>Roles a restaurant can assign to its own staff (owners are created at onboarding).</summary>
    public static readonly IReadOnlyList<string> StaffRoles =
    [
        PlatformRoles.RestaurantManager,
        PlatformRoles.Waiter,
        PlatformRoles.KitchenStaff,
        PlatformRoles.Cashier,
        PlatformRoles.RestaurantStaff,
        PlatformRoles.MarketingUser,
    ];

    public static bool IsKnownRole(string role) => ByRole.ContainsKey(role);

    public static IReadOnlyList<string> For(string role) =>
        ByRole.TryGetValue(role, out var permissions) ? permissions : [];
}
