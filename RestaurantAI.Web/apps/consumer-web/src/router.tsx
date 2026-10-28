import { RequireAuth } from "@restaurant-ai/auth";
import { createBrowserRouter } from "react-router";
import { ConsumerLayout } from "./layout/ConsumerLayout";
import { VenueLayout } from "./layout/VenueLayout";
import { AccountPage } from "./pages/AccountPage";
import { CartPage } from "./pages/CartPage";
import { DiscoverPage } from "./pages/DiscoverPage";
import { FavoritesPage } from "./pages/FavoritesPage";
import { LoginPage } from "./pages/LoginPage";
import { MenuPage } from "./pages/MenuPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { OrderPage } from "./pages/OrderPage";
import { ReservationPage } from "./pages/ReservationPage";
import { ReservePage } from "./pages/ReservePage";

export const router = createBrowserRouter([
  {
    element: <ConsumerLayout />,
    children: [
      // Public: discovery works without signing in.
      { index: true, element: <DiscoverPage /> },
      { path: "login", element: <LoginPage /> },
      { path: "favorites", element: <RequireAuth><FavoritesPage /></RequireAuth> },
      {
        // QR entry point and "View menu": /r/<restaurant>/<branch>?table=<table>
        path: "r/:tenantId/:branchId",
        element: <VenueLayout />,
        children: [
          { index: true, element: <MenuPage /> },
          { path: "cart", element: <CartPage /> },
          { path: "reserve", element: <ReservePage /> },
        ],
      },
      { path: "orders/:orderId", element: <RequireAuth><OrderPage /></RequireAuth> },
      { path: "reservations/:reservationId", element: <RequireAuth><ReservationPage /></RequireAuth> },
      { path: "account", element: <RequireAuth><AccountPage /></RequireAuth> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
