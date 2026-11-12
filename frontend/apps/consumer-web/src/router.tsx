import { RequireAuth } from "@restaurant-ai/auth";
import { createBrowserRouter } from "react-router";
import { ConsumerLayout } from "./shared/ConsumerLayout";
import { VenueLayout } from "./shared/VenueLayout";
import { AccountPage } from "./menus/account/AccountPage";
import { CartPage } from "./menus/cart/CartPage";
import { DiscoverPage } from "./menus/discover/DiscoverPage";
import { FavoritesPage } from "./menus/my-restaurants/FavoritesPage";
import { LoginPage, RegisterPage } from "./auth/LoginPage";
import { MenuPage } from "./menus/current-menu/MenuPage";
import { MyOrdersPage } from "./menus/my-orders/MyOrdersPage";
import { NotFoundPage } from "./shared/NotFoundPage";
import { OrderPage } from "./menus/my-orders/OrderPage";
import { QrEntryPage } from "./auth/QrEntryPage";
import { ReservationPage } from "./menus/current-menu/ReservationPage";
import { ReservePage } from "./menus/current-menu/ReservePage";

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
