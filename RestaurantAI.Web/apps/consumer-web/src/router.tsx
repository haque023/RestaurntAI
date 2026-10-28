import { RequireAuth } from "@restaurant-ai/auth";
import { createBrowserRouter } from "react-router";
import { ConsumerLayout } from "./layout/ConsumerLayout";
import { VenueLayout } from "./layout/VenueLayout";
import { AccountPage } from "./pages/AccountPage";
import { CartPage } from "./pages/CartPage";
import { DiscoverPage } from "./pages/DiscoverPage";
import { FavoritesPage } from "./pages/FavoritesPage";
import { LoginPage, RegisterPage } from "./pages/LoginPage";
import { MenuPage } from "./pages/MenuPage";
import { MyOrdersPage } from "./pages/MyOrdersPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { OrderPage } from "./pages/OrderPage";
import { QrEntryPage } from "./pages/QrEntryPage";
import { ReservationPage } from "./pages/ReservationPage";
import { ReservePage } from "./pages/ReservePage";

export const router = createBrowserRouter([
  {
    element: <ConsumerLayout />,
    children: [
      // Public: discovery works without signing in.
      { index: true, element: <DiscoverPage /> },
      { path: "login", element: <LoginPage /> },
      { path: "register", element: <RegisterPage /> },
      // Printed table QR codes point here: /t/<code>
      { path: "t/:code", element: <QrEntryPage /> },
      { path: "favorites", element: <RequireAuth><FavoritesPage /></RequireAuth> },
      {
        // A restaurant branch: "Order here" from discovery, or after a table QR scan.
        path: "r/:tenantId/:branchId",
        element: <VenueLayout />,
        children: [
          { index: true, element: <MenuPage /> },
          { path: "cart", element: <CartPage /> },
          { path: "reserve", element: <ReservePage /> },
          { path: "orders", element: <MyOrdersPage /> },
        ],
      },
      { path: "orders/:orderId", element: <RequireAuth><OrderPage /></RequireAuth> },
      { path: "reservations/:reservationId", element: <RequireAuth><ReservationPage /></RequireAuth> },
      { path: "account", element: <RequireAuth><AccountPage /></RequireAuth> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
