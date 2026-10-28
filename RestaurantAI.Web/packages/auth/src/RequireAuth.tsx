import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router";
import { useAuth } from "./context";

export interface RequireAuthProps {
  children: ReactNode;
  /** All listed permissions are required. */
  permissions?: string[];
  /** At least one of the listed roles is required. */
  anyRole?: string[];
  loginPath?: string;
  forbidden?: ReactNode;
}

/** Route guard for UX only; the APIs enforce the same rules. */
export function RequireAuth({ children, permissions = [], anyRole, loginPath = "/login", forbidden }: RequireAuthProps) {
  const { isAuthenticated, hasPermission, hasRole } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to={loginPath} replace state={{ from: location.pathname + location.search }} />;
  }

  const allowed = permissions.every(hasPermission) && (!anyRole || anyRole.some(hasRole));
  if (!allowed) {
    return forbidden ?? <p role="alert">You do not have access to this page.</p>;
  }

  return <>{children}</>;
}
