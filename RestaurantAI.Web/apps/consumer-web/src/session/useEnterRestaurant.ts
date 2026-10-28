import { useAuth } from "@restaurant-ai/auth";
import { useCallback } from "react";
import { customerPersona } from "../personas";

/**
 * Re-issues the guest's token scoped to a restaurant (tenant) when they open its menu.
 * Ordering, reservations and loyalty are tenant-scoped; discovery and favorites are not.
 * Development uses the dev-token endpoint; production would use a token exchange with the
 * identity provider for the same user.
 */
export function useEnterRestaurant() {
  const { api, user, signIn } = useAuth();

  return useCallback(
    async (tenantId: string) => {
      if (!user) throw new Error("Sign in first.");
      if (user.tenantId === tenantId) return user;

      const { access_token } = await api.auth.devToken({
        userId: user.userId,
        tenantId,
        roles: customerPersona.roles,
        permissions: customerPersona.permissions,
        lifetimeMinutes: 480,
      });
      return signIn(access_token);
    },
    [api, user, signIn],
  );
}
