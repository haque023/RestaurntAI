import { Permissions } from "@restaurant-ai/api-client";
import { RequireAuth } from "@restaurant-ai/auth";
import { EmptyState } from "@restaurant-ai/ui";
import type { ReactNode } from "react";
import { createBrowserRouter, Link } from "react-router";
import { Layout } from "./shared/Layout";
import { AiPage } from "./menus/ai-waiter/AiPage";
import { DashboardPage } from "./menus/dashboard/DashboardPage";
import { Home } from "./shared/Home";
import { LoginPage, SignupPage } from "./auth/LoginPage";
import { MenuPage } from "./menus/menu/MenuPage";
import { OrdersBoardPage } from "./menus/orders/OrdersBoardPage";
import { ReservationsPage } from "./menus/reservations/ReservationsPage";
import { RestaurantPage } from "./menus/restaurant/RestaurantPage";
import { StaffPage } from "./menus/staff/StaffPage";
import { TablesPage } from "./menus/tables/TablesPage";

const guard = (permission: string, page: ReactNode) => <RequireAuth permissions={[permission]}>{page}</RequireAuth>;

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: "login", element: <LoginPage /> },
      { path: "signup", element: <SignupPage /> },
      { index: true, element: <Home /> },
      { path: "dashboard", element: guard(Permissions.AnalyticsRead, <DashboardPage />) },
      { path: "orders", element: guard(Permissions.OrdersManage, <OrdersBoardPage />) },
      { path: "tables", element: guard(Permissions.OrdersRead, <TablesPage />) },
      { path: "reservations", element: guard(Permissions.ReservationsManage, <ReservationsPage />) },
      { path: "menu", element: guard(Permissions.MenuWrite, <MenuPage />) },
      { path: "staff", element: guard(Permissions.StaffManage, <StaffPage />) },
      { path: "ai", element: guard(Permissions.AiManage, <AiPage />) },
      { path: "restaurant", element: guard(Permissions.RestaurantManage, <RestaurantPage />) },
      { path: "*", element: <EmptyState title="Page not found"><Link to="/">Go to the console</Link></EmptyState> },
    ],
  },
]);
