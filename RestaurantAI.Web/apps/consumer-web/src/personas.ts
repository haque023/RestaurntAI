import { Permissions, PlatformRoles } from "@restaurant-ai/api-client";
import type { DevPersona } from "@restaurant-ai/auth";

/** Guests sign in once for the whole platform; a restaurant is chosen when they open its menu. */
export const customerPersona: DevPersona = {
  label: "Customer",
  roles: [PlatformRoles.Customer],
  permissions: [
    Permissions.MenuRead,
    Permissions.OrdersCreate,
    Permissions.OrdersRead,
    Permissions.OrdersCancel,
    Permissions.ReservationsCreate,
    Permissions.ReservationsRead,
    Permissions.ReservationsCancel,
    Permissions.LoyaltyRead,
    Permissions.ConsumerProfile,
  ],
  needsTenant: false,
};

const DEV_USER_KEY = "rai.consumer.devUserId";

/**
 * Development only: one stable user id per browser, so favorites, orders and loyalty belong to
 * the same person across sign-ins. The real identity provider supplies this in production.
 */
export function devUserId(): string {
  try {
    let id = localStorage.getItem(DEV_USER_KEY);
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem(DEV_USER_KEY, id);
    }
    return id;
  } catch {
    return crypto.randomUUID();
  }
}
