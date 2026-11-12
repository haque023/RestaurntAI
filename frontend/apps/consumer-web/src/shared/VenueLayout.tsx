import { useAuth } from "@restaurant-ai/auth";
import { Button, ErrorAlert, Spinner } from "@restaurant-ai/ui";
import { useEffect, useState } from "react";
import { Outlet, useParams } from "react-router";
import { CartProvider } from "../menus/cart/CartContext";
import { useEnterRestaurant } from "../auth/useEnterRestaurant";
import { useVenueTable } from "../auth/useVenueTable";
import { rememberVenue } from "./venue";

/**
 * Wraps every page under /r/:tenantId/:branchId ("Order here" from discovery, or after a QR scan).
 * The token is scoped to this restaurant and branch automatically; no sign-in is required.
 */
export function VenueLayout() {
  const { tenantId = "", branchId = "" } = useParams();
  const { user } = useAuth();
  const tableId = useVenueTable();
  const enterRestaurant = useEnterRestaurant();
  const [error, setError] = useState<unknown>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    rememberVenue({ tenantId, branchId, tableId });
  }, [tenantId, branchId, tableId]);

  const needsSwitch = !user || user.tenantId !== tenantId || (user.branchId !== null && user.branchId !== branchId);

  useEffect(() => {
    if (!needsSwitch) return;
    let cancelled = false;
    enterRestaurant(tenantId, branchId).catch((e: unknown) => {
      if (!cancelled) setError(e);
    });
    return () => {
      cancelled = true;
    };
  }, [needsSwitch, tenantId, branchId, enterRestaurant, attempt]);

  if (needsSwitch) {
    return error ? (
      <div className="stack">
        <ErrorAlert error={error} title="Could not open this restaurant" />
        <div>
          <Button
            onClick={() => {
              setError(null);
              setAttempt((n) => n + 1);
            }}
          >
            Try again
          </Button>
        </div>
      </div>
    ) : (
      <Spinner label="Opening restaurant" />
    );
  }

  return (
    <CartProvider branchId={branchId}>
      <Outlet />
    </CartProvider>
  );
}
