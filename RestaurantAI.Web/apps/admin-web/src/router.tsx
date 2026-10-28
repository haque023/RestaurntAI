import { PlatformRoles } from "@restaurant-ai/api-client";
import { RequireAuth } from "@restaurant-ai/auth";
import { EmptyState } from "@restaurant-ai/ui";
import type { ReactNode } from "react";
import { createBrowserRouter, Link } from "react-router";
import { Layout } from "./Layout";
import { AiGovernancePage } from "./pages/AiGovernancePage";
import { AuditPage } from "./pages/AuditPage";
import { LoginPage } from "./pages/LoginPage";
import { OnboardRestaurantPage } from "./pages/OnboardRestaurantPage";
import { OverviewPage } from "./pages/OverviewPage";
import { RestaurantDetailPage, RestaurantsPage } from "./pages/RestaurantsPage";
import { UsersPage } from "./pages/UsersPage";

const admin = (page: ReactNode) => <RequireAuth anyRole={[PlatformRoles.PlatformAdministrator]}>{page}</RequireAuth>;

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: "login", element: <LoginPage /> },
      { index: true, element: admin(<OverviewPage />) },
      { path: "restaurants", element: admin(<RestaurantsPage />) },
      { path: "restaurants/new", element: admin(<OnboardRestaurantPage />) },
      { path: "restaurants/:restaurantId", element: admin(<RestaurantDetailPage />) },
      { path: "ai", element: admin(<AiGovernancePage />) },
      { path: "audit", element: admin(<AuditPage />) },
      { path: "users", element: admin(<UsersPage />) },
      { path: "*", element: <EmptyState title="Page not found"><Link to="/">Go to overview</Link></EmptyState> },
    ],
  },
]);
