import { Permissions, PlatformRoles } from "@restaurant-ai/api-client";
import type { DevPersona } from "@restaurant-ai/auth";

export const managerPersona: DevPersona = {
  label: "Restaurant manager",
  roles: [PlatformRoles.RestaurantManager],
  permissions: [
    Permissions.RestaurantManage,
    Permissions.MenuRead,
    Permissions.MenuWrite,
    Permissions.OrdersRead,
    Permissions.OrdersCancel,
    Permissions.OrdersManage,
    Permissions.ReservationsRead,
    Permissions.ReservationsCancel,
    Permissions.ReservationsManage,
    Permissions.LoyaltyRead,
  ],
  needsTenant: true,
  allowsBranch: true,
};
