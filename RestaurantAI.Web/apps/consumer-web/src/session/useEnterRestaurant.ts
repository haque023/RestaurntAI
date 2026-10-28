import { useAuth } from "@restaurant-ai/auth";
import { useCallback } from "react";

/**
 * Scopes the guest's token to a restaurant (and branch) when they open its menu. Signed-in
 * customers keep their identity; visitors without an account get a temporary guest identity,
 * so they can browse, order and talk to the AI waiter straight away.
 */
export function useEnterRestaurant() {
  const { api, signIn } = useAuth();

  return useCallback(
    async (tenantId: string, branchId: string) => {
      const session = await api.auth.venueSession({ tenantId, branchId });
      return signIn(session.accessToken);
    },
    [api, signIn],
  );
}
