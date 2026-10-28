import { createApiClient, PlatformRoles } from "@restaurant-ai/api-client";
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { parseToken } from "./claims";
import { AuthContext, type AuthContextValue } from "./context";
import { TokenStore } from "./tokenStore";

export interface AuthProviderProps {
  /** sessionStorage key; one per app so personas do not leak between apps. */
  storageKey: string;
  /** Gateway base URL. Defaults to same origin (Vite dev proxy / edge). */
  apiBaseUrl?: string;
  children: ReactNode;
}

/**
 * Holds the access token for the current tab and exposes an API client bound to it.
 * Tokens live in sessionStorage (cleared when the tab closes); production should move to
 * the identity provider's SDK (OIDC authorization code flow + PKCE).
 */
export function AuthProvider({ storageKey, apiBaseUrl, children }: AuthProviderProps) {
  const [store] = useState(() => new TokenStore(storageKey));
  const [token, setToken] = useState(() => store.get());

  const signOut = useCallback(() => {
    store.set(null);
    setToken(null);
  }, [store]);

  const signIn = useCallback(
    (next: string) => {
      const user = parseToken(next);
      if (!user) throw new Error("The access token could not be read.");
      store.set(next);
      setToken(next);
      return user;
    },
    [store],
  );

  const api = useMemo(
    () => createApiClient({ baseUrl: apiBaseUrl, getAccessToken: () => store.get(), onUnauthorized: signOut }),
    [apiBaseUrl, store, signOut],
  );

  const value = useMemo<AuthContextValue>(() => {
    const user = token ? parseToken(token) : null;
    const isAdmin = user?.roles.includes(PlatformRoles.PlatformAdministrator) ?? false;
    return {
      user,
      token,
      isAuthenticated: user !== null,
      signIn,
      signOut,
      hasPermission: (permission) => isAdmin || (user?.permissions.includes(permission) ?? false),
      hasRole: (role) => user?.roles.includes(role) ?? false,
      api,
    };
  }, [token, signIn, signOut, api]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
