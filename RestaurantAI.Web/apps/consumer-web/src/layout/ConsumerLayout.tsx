import { useAuth } from "@restaurant-ai/auth";
import { AppShell, type NavItem } from "@restaurant-ai/ui";
import { Link, Outlet } from "react-router";
import { lastVenue, venuePath } from "../venue";

export function ConsumerLayout() {
  const { user, signOut } = useAuth();
  const venue = lastVenue();

  const nav: NavItem[] = [
    { to: "/", label: "Discover", end: true },
    ...(user ? [{ to: "/favorites", label: "My restaurants" }] : []),
    ...(user && venue ? [{ to: venuePath(venue), label: "Current menu", end: true }, { to: venuePath(venue, "cart"), label: "Cart" }] : []),
    ...(user ? [{ to: "/account", label: "Account" }] : []),
  ];

  return (
    <AppShell appName="RestaurantAI" nav={nav} user={user ? { label: "Signed in", detail: user.userId } : null} onSignOut={signOut}>
      {!user && (
        <div className="row" style={{ justifyContent: "flex-end", marginBottom: -12 }}>
          <Link className="btn btn-sm" to="/login">
            Sign in
          </Link>
        </div>
      )}
      <Outlet />
    </AppShell>
  );
}
