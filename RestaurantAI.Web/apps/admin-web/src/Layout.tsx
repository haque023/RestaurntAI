import { useAuth } from "@restaurant-ai/auth";
import { AppShell } from "@restaurant-ai/ui";
import { Outlet } from "react-router";

export function Layout() {
  const { user, signOut } = useAuth();
  return (
    <AppShell
      appName="Platform Admin"
      nav={
        user
          ? [
              { to: "/", label: "Overview", end: true },
              { to: "/restaurants", label: "Restaurants" },
              { to: "/ai", label: "AI governance" },
              { to: "/audit", label: "Audit log" },
              { to: "/users", label: "Users" },
            ]
          : []
      }
      user={user ? { label: user.displayName ?? user.email ?? "Administrator", detail: user.email ?? undefined } : null}
      onSignOut={signOut}
    >
      <Outlet />
    </AppShell>
  );
}
