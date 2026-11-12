import type { ApiClient } from "@restaurant-ai/api-client";
import { createContext, useContext } from "react";
import type { SessionUser } from "./claims";

export interface AuthContextValue {
  user: SessionUser | null;
  token: string | null;
  isAuthenticated: boolean;
  signIn: (token: string) => SessionUser;
  signOut: () => void;
  hasPermission: (permission: string) => boolean;
  hasRole: (role: string) => boolean;
  api: ApiClient;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside <AuthProvider>.");
  return context;
}

export function useApi(): ApiClient {
  return useAuth().api;
}
