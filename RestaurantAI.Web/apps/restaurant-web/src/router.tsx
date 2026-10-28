import { Permissions } from "@restaurant-ai/api-client";
import { RequireAuth } from "@restaurant-ai/auth";
import { EmptyState } from "@restaurant-ai/ui";
import { createBrowserRouter, Link } from "react-router";
import { Layout } from "./Layout";
import { LoginPage } from "./pages/LoginPage";
import { OrdersPage, ReservationsPage } from "./pages/LookupPages";
import { MenuPage } from "./pages/MenuPage";
import { RestaurantPage } from "./pages/RestaurantPage";

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: "login", element: <LoginPage /> },
      { index: true, element: <RequireAuth permissions={[Permissions.RestaurantManage]}><RestaurantPage /></RequireAuth> },
      { path: "menu", element: <RequireAuth permissions={[Permissions.MenuWrite]}><MenuPage /></RequireAuth> },
      { path: "orders", element: <RequireAuth permissions={[Permissions.OrdersRead]}><OrdersPage /></RequireAuth> },
      { path: "reservations", element: <RequireAuth permissions={[Permissions.ReservationsRead]}><ReservationsPage /></RequireAuth> },
      { path: "*", element: <EmptyState title="Page not found"><Link to="/">Go to the console</Link></EmptyState> },
    ],
  },
]);
