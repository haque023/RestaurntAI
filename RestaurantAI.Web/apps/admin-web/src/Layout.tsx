import { useAuth } from "@restaurant-ai/auth";
import { AppShell } from "@restaurant-ai/ui";
import { Outlet } from "react-router";

export function Layout() {
  const { user, signOut } = useAuth();
  return (
    <AppShell
      appName="Platform Admin"
      nav={user ? [{ to: "/", label: "Overview", end: true }, { to: "/restaurants/new", label: "Register restaurant" }] : []}
      user={user ? { label: "Administrator", detail: user.userId } : null}
      onSignOut={signOut}
    >
      <Outlet />
    </AppShell>
  );
}
