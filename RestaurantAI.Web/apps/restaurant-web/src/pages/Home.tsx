import { Permissions } from "@restaurant-ai/api-client";
import { useAuth } from "@restaurant-ai/auth";
import { Navigate } from "react-router";

/** Owners land on the dashboard, floor and kitchen staff on the order board. */
export function Home() {
  const { isAuthenticated, hasPermission } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (hasPermission(Permissions.AnalyticsRead)) return <Navigate to="/dashboard" replace />;
  if (hasPermission(Permissions.OrdersManage)) return <Navigate to="/orders" replace />;
  return <Navigate to="/menu" replace />;
}
