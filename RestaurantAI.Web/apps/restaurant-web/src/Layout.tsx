import { useAuth } from "@restaurant-ai/auth";
import { AppShell } from "@restaurant-ai/ui";
import { Outlet } from "react-router";

export function Layout() {
  const { user, signOut } = useAuth();
  return (
    <AppShell
      appName="Restaurant Console"
      nav={
        user
          ? [
              { to: "/", label: "Restaurant", end: true },
              { to: "/menu", label: "Menu" },
              { to: "/orders", label: "Orders" },
              { to: "/reservations", label: "Reservations" },
            ]
          : []
      }
      user={user ? { label: user.branchId ? "Branch staff" : "Manager", detail: `tenant ${user.tenantId}` } : null}
      onSignOut={signOut}
    >
      <Outlet />
    </AppShell>
  );
}
