import { createHttp, type HttpClientOptions } from "./http";
import type {
  AddBranchRequest,
  AdminOverview,
  Branch,
  CreateMenuItemRequest,
  CreateReservationRequest,
  CustomerProfile,
  DevTokenRequest,
  DevTokenResponse,
  DiscoveryBranch,
  DiscoverySearch,
  DishResult,
  DishSearch,
  FavoriteRestaurant,
  Guid,
  LoyaltyBalance,
  MenuAvailability,
  MenuItem,
  MenuSearch,
  Order,
  PlaceOrderRequest,
  RegisterRestaurantRequest,
  Reservation,
  Restaurant,
  UpdateBranchRequest,
  UpdateProfileRequest,
  UpdateRestaurantProfileRequest,
  WaiterTurnRequest,
  WaiterTurnResponse,
} from "./types";

/** Typed client for the platform APIs, all reached through the YARP gateway. */
export function createApiClient(options: HttpClientOptions = {}) {
  const http = createHttp(options);

  return {
    auth: {
      /** Development only: the Identity service issues local tokens. */
      devToken: (request: DevTokenRequest) => http<DevTokenResponse>("POST", "/api/auth/dev-token", { body: request }),
      me: () => http<Record<string, unknown>>("GET", "/api/auth/me"),
    },

    restaurants: {
      register: (request: RegisterRestaurantRequest) => http<Restaurant>("POST", "/api/restaurants", { body: request }),
      current: () => http<Restaurant>("GET", "/api/restaurants/current"),
      addBranch: (request: AddBranchRequest) => http<Branch>("POST", "/api/restaurants/current/branches", { body: request }),
      updateProfile: (request: UpdateRestaurantProfileRequest) => http<Restaurant>("PUT", "/api/restaurants/current/profile", { body: request }),
      setListing: (listed: boolean) => http<Restaurant>("PUT", "/api/restaurants/current/listing", { body: { listed } }),
      updateBranch: (branchId: Guid, request: UpdateBranchRequest) =>
        http<Branch>("PUT", `/api/restaurants/current/branches/${branchId}`, { body: request }),
    },

    /** Public discovery; works without signing in. */
    discovery: {
      restaurants: (search: DiscoverySearch, signal?: AbortSignal) =>
        http<DiscoveryBranch[]>("GET", "/api/discovery/restaurants", {
          query: {
            q: search.q,
            cuisines: search.cuisines,
            diets: search.diets,
            lat: search.lat,
            lng: search.lng,
            radiusKm: search.radiusKm,
            openNow: search.openNow || undefined,
            branchIds: search.branchIds,
            tenantIds: search.tenantIds,
            limit: search.limit,
          },
          signal,
        }),
      cuisines: (signal?: AbortSignal) => http<string[]>("GET", "/api/discovery/restaurants/cuisines", { signal }),
      dishes: (search: DishSearch, signal?: AbortSignal) =>
        http<DishResult[]>("GET", "/api/discovery/menu-items", {
          query: { q: search.q, diets: search.diets, maxPrice: search.maxPrice, limit: search.limit },
          signal,
        }),
    },

    menu: {
      list: (branchId: Guid, signal?: AbortSignal) =>
        http<MenuItem[]>("GET", `/api/menu/branches/${branchId}/items`, { signal }),
      search: (branchId: Guid, search: MenuSearch, signal?: AbortSignal) =>
        http<MenuItem[]>("GET", `/api/menu/branches/${branchId}/items/search`, {
          query: { q: search.q, maxPrice: search.maxPrice, dietary: search.dietary, limit: search.limit },
          signal,
        }),
      availability: (itemId: Guid) => http<MenuAvailability>("GET", `/api/menu/items/${itemId}/availability`),
      create: (branchId: Guid, request: CreateMenuItemRequest) =>
        http<MenuItem>("POST", `/api/menu/branches/${branchId}/items`, { body: { ...request, branchId } }),
      setAvailability: (itemId: Guid, isAvailable: boolean) =>
        http<MenuItem>("PUT", `/api/menu/items/${itemId}/availability`, { body: { isAvailable } }),
    },

    orders: {
      /** Requires an idempotency key; reuse the same key when retrying the same confirmation. */
      place: (request: PlaceOrderRequest, idempotencyKey: string) =>
        http<Order>("POST", "/api/orders", { body: request, idempotencyKey }),
      get: (orderId: Guid, signal?: AbortSignal) => http<Order>("GET", `/api/orders/${orderId}`, { signal }),
      cancel: (orderId: Guid, reason: string, idempotencyKey?: string) =>
        http<Order>("POST", `/api/orders/${orderId}/cancel`, { body: { reason }, idempotencyKey }),
    },

    reservations: {
      create: (request: CreateReservationRequest, idempotencyKey: string) =>
        http<Reservation>("POST", "/api/reservations", { body: request, idempotencyKey }),
      get: (reservationId: Guid, signal?: AbortSignal) =>
        http<Reservation>("GET", `/api/reservations/${reservationId}`, { signal }),
      cancel: (reservationId: Guid, reason: string, idempotencyKey?: string) =>
        http<Reservation>("POST", `/api/reservations/${reservationId}/cancel`, { body: { reason }, idempotencyKey }),
    },

    consumer: {
      profile: () => http<CustomerProfile>("GET", "/api/consumer/profile"),
      updateProfile: (request: UpdateProfileRequest) => http<CustomerProfile>("PUT", "/api/consumer/profile", { body: request }),
      favorites: () => http<FavoriteRestaurant[]>("GET", "/api/consumer/favorites"),
      addFavorite: (restaurantId: Guid) => http<FavoriteRestaurant>("PUT", `/api/consumer/favorites/${restaurantId}`),
      removeFavorite: (restaurantId: Guid) => http<void>("DELETE", `/api/consumer/favorites/${restaurantId}`),
    },

    loyalty: {
      balance: () => http<LoyaltyBalance>("GET", "/api/loyalty/balance"),
    },

    ai: {
      /** One turn with the AI waiter. It acts through the same APIs with the guest's own permissions. */
      waiterTurn: (request: WaiterTurnRequest, signal?: AbortSignal) =>
        http<WaiterTurnResponse>("POST", "/api/ai/waiter/turns", { body: request, signal }),
    },

    admin: {
      overview: () => http<AdminOverview>("GET", "/api/admin/overview"),
    },
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
