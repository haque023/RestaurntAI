import { useAuth } from "@restaurant-ai/auth";
import { useParams } from "react-router";

/** The table the guest is seated at: only a QR session (table claim in the token) seats them. */
export function useVenueTable(): string | null {
  const { user } = useAuth();
  const { tenantId = "" } = useParams();
  return user?.tenantId === tenantId ? user.tableId : null;
}
