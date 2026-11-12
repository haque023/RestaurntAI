import { Permissions, RoleLabels } from "@restaurant-ai/api-client";
import { useAuth } from "@restaurant-ai/auth";
import { AppShell, type NavItem } from "@restaurant-ai/ui";
import { Outlet } from "react-router";
import { useRestaurantSummary } from "./useRestaurant";

const NAV: (NavItem & { permission: string })[] = [
  { to: "/dashboard", label: "Dashboard", permission: Permissions.AnalyticsRead },
  { to: "/orders", label: "Orders", permission: Permissions.OrdersManage },
  { to: "/tables", label: "Tables & QR", permission: Permissions.OrdersRead },
  { to: "/reservations", label: "Reservations", permission: Permissions.ReservationsManage },
  { to: "/menu", label: "Menu", permission: Permissions.MenuWrite },
  { to: "/staff", label: "Staff", permission: Permissions.StaffManage },
  { to: "/ai", label: "AI waiter", permission: Permissions.AiManage },
  { to: "/restaurant", label: "Restaurant", permission: Permissions.RestaurantManage },
];

export function Layout() {
  const { user, signOut, hasPermission } = useAuth();
  const summary = useRestaurantSummary();
  const status = summary.data?.status;

  const roles = user?.roles.map((r) => RoleLabels[r] ?? r).join(", ");
  const label = user ? `${user.displayName ?? user.email ?? "Staff"} · ${roles}` : null;

  return (
    <AppShell
      appName={summary.data?.name ?? "Restaurant Console"}
      homePath="/"
      nav={user ? NAV.filter((item) => hasPermission(item.permission)) : []}
      user={label ? { label, detail: user?.email ?? undefined } : null}
      onSignOut={signOut}
    >
      {status === "PendingVerification" && (
        <div className="alert alert-info no-print" role="status">
          <strong>Awaiting verification.</strong> The platform team is reviewing your restaurant. Set up your menu, tables and staff now; you can list it
          for guests once it is approved.
        </div>
      )}
      {status === "Suspended" && (
        <div className="alert alert-error no-print" role="alert">
          <strong>Suspended.</strong> This restaurant is hidden from guests and its AI waiter is off. Contact platform support.
        </div>
      )}
      <Outlet />
    </AppShell>
  );
}
