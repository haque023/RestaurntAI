import { useAuth } from "@restaurant-ai/auth";
import { AppShell, type NavItem } from "@restaurant-ai/ui";
import { Link, Outlet, useLocation } from "react-router";
import { lastVenue, venuePath } from "./venue";

export function ConsumerLayout() {
  const { user, signOut } = useAuth();
  const location = useLocation();
  const venue = lastVenue();
  const member = user !== null && !user.isGuest;

  const nav: NavItem[] = [
    { to: "/", label: "Discover", end: true },
    ...(member ? [{ to: "/favorites", label: "My restaurants" }] : []),
    ...(user && venue
      ? [
          { to: venuePath(venue), label: "Current menu", end: true },
          { to: venuePath(venue, "cart"), label: "Cart" },
          { to: venuePath(venue, "orders"), label: "My orders" },
        ]
      : []),
    ...(member ? [{ to: "/account", label: "Account" }] : []),
  ];

  const label = !user ? null : user.isGuest ? "Guest" : (user.displayName ?? user.email ?? "Signed in");

  return (
    <AppShell appName="RestaurantAI" nav={nav} user={label ? { label, detail: user?.email ?? undefined } : null} onSignOut={signOut}>
      {!member && (
        <div className="row" style={{ justifyContent: "flex-end", marginBottom: -12 }}>
          {user?.isGuest && <span className="small muted">Ordering as a guest.</span>}
          <Link className="btn btn-sm" to="/login" state={{ from: location.pathname + location.search }}>
            Sign in
          </Link>
          <Link className="btn btn-sm btn-primary" to="/register" state={{ from: location.pathname + location.search }}>
            Create account
          </Link>
        </div>
      )}
      <Outlet />
    </AppShell>
  );
}
