import { PlatformRoles } from "@restaurant-ai/api-client";
import { RequireAuth } from "@restaurant-ai/auth";
import { EmptyState } from "@restaurant-ai/ui";
import { createBrowserRouter, Link } from "react-router";
import { Layout } from "./Layout";
import { LoginPage } from "./pages/LoginPage";
import { OverviewPage } from "./pages/OverviewPage";
import { RegisterRestaurantPage } from "./pages/RegisterRestaurantPage";

const adminOnly = [PlatformRoles.PlatformAdministrator];

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: "login", element: <LoginPage /> },
      { index: true, element: <RequireAuth anyRole={adminOnly}><OverviewPage /></RequireAuth> },
      { path: "restaurants/new", element: <RequireAuth anyRole={adminOnly}><RegisterRestaurantPage /></RequireAuth> },
      { path: "*", element: <EmptyState title="Page not found"><Link to="/">Go to overview</Link></EmptyState> },
    ],
  },
]);
