import { Permissions } from "@restaurant-ai/api-client";
import { RequireAuth } from "@restaurant-ai/auth";
import { EmptyState } from "@restaurant-ai/ui";
import type { ReactNode } from "react";
import { createBrowserRouter, Link } from "react-router";
import { Layout } from "./Layout";
import { AiPage } from "./pages/AiPage";
import { DashboardPage } from "./pages/DashboardPage";
import { Home } from "./pages/Home";
import { LoginPage, SignupPage } from "./pages/LoginPage";
import { MenuPage } from "./pages/MenuPage";
import { OrdersBoardPage } from "./pages/OrdersBoardPage";
import { ReservationsPage } from "./pages/ReservationsPage";
import { RestaurantPage } from "./pages/RestaurantPage";
import { StaffPage } from "./pages/StaffPage";
import { TablesPage } from "./pages/TablesPage";

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
