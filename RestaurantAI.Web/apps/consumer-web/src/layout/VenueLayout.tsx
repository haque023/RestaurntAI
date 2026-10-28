import { useAuth } from "@restaurant-ai/auth";
import { Button, ErrorAlert, Spinner } from "@restaurant-ai/ui";
import { useEffect, useState } from "react";
import { Navigate, Outlet, useLocation, useParams, useSearchParams } from "react-router";
import { CartProvider } from "../cart/CartContext";
import { useEnterRestaurant } from "../session/useEnterRestaurant";
import { rememberVenue } from "../venue";

/**
 * Wraps every page under /r/:tenantId/:branchId (QR links and "View menu" from discovery).
 * The guest's token is switched to this restaurant automatically.
 */
export function VenueLayout() {
  const { tenantId = "", branchId = "" } = useParams();
  const [search] = useSearchParams();
  const tableId = search.get("table");
  const { user } = useAuth();
  const location = useLocation();
  const enterRestaurant = useEnterRestaurant();
  const [error, setError] = useState<unknown>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    rememberVenue({ tenantId, branchId, tableId });
  }, [tenantId, branchId, tableId]);

  const needsSwitch = user !== null && user.tenantId !== tenantId;

  useEffect(() => {
    if (!needsSwitch) return;
    let cancelled = false;
    enterRestaurant(tenantId).catch((e: unknown) => {
      if (!cancelled) setError(e);
    });
    return () => {
      cancelled = true;
    };
  }, [needsSwitch, tenantId, enterRestaurant, attempt]);

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

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
