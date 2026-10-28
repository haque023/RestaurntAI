import { createHttp, type HttpClientOptions } from "./http";
import type {
  AddBranchRequest,
  AddStaffRequest,
  AdminOverview,
  AdminRestaurantStats,
  AdminRestaurantSummary,
  AdminUser,
  AgentRegistry,
  AiSettings,
  AuditEvent,
  AuditQuery,
  Branch,
  CreateMenuItemRequest,
  CreateReservationRequest,
  CustomerProfile,
  DevTokenRequest,
  DevTokenResponse,
  DiningTable,
  DiscoveryBranch,
  DiscoverySearch,
  DiscoveryTurnRequest,
  DiscoveryTurnResponse,
  DishResult,
  DishSearch,
  FavoriteRestaurant,
  Guid,
  LoginRequest,
  LoyaltyBalance,
  Membership,
  MenuAvailability,
  MenuItem,
  MenuSearch,
  Order,
  OrderStats,
  OrderStatus,
  PlaceOrderRequest,
  PlatformAiUsage,
  QrVenue,
  RegisterRequest,
  RegisterRestaurantRequest,
  Reservation,
  Restaurant,
  RestaurantAiUsage,
  RestaurantSignupRequest,
  RestaurantSummary,
  RestaurantStatus,
  ServiceRequest,
  ServiceRequestKind,
  SessionResponse,
  StaffMember,
  SubscriptionPlan,
  TableStatus,
  UpdateBranchRequest,
  UpdateMenuItemRequest,
  UpdateProfileRequest,
  UpdateRestaurantProfileRequest,
  VenueSessionRequest,
  WaiterTurnRequest,
  WaiterTurnResponse,
} from "./types";

/** Typed client for the platform APIs, all reached through the YARP gateway. */
export function createApiClient(options: HttpClientOptions = {}) {
  const http = createHttp(options);

  return {
    auth: {
      login: (request: LoginRequest) => http<SessionResponse>("POST", "/api/auth/login", { body: request }),
      register: (request: RegisterRequest) => http<SessionResponse>("POST", "/api/auth/register", { body: request }),
      signupRestaurant: (request: RestaurantSignupRequest) =>
        http<SessionResponse>("POST", "/api/auth/signup/restaurant", { body: request }),
      /** Scopes the caller (or an anonymous QR guest) to one restaurant, branch and table. */
      venueSession: (request: VenueSessionRequest) => http<SessionResponse>("POST", "/api/auth/venue-session", { body: request }),
      switchTenant: (tenantId: Guid) => http<SessionResponse>("POST", "/api/auth/switch", { body: { tenantId } }),
      memberships: () => http<Membership[]>("GET", "/api/auth/memberships"),
      /** Development only: the Identity service issues arbitrary local tokens for test scripts. */
      devToken: (request: DevTokenRequest) => http<DevTokenResponse>("POST", "/api/auth/dev-token", { body: request }),
      me: () => http<Record<string, unknown>>("GET", "/api/auth/me"),
    },

    staff: {
      list: () => http<StaffMember[]>("GET", "/api/auth/staff"),
      add: (request: AddStaffRequest) => http<StaffMember>("POST", "/api/auth/staff", { body: request }),
      update: (userId: Guid, role: string, branchId: Guid | null) =>
        http<StaffMember>("PUT", `/api/auth/staff/${userId}`, { body: { role, branchId } }),
      remove: (userId: Guid) => http<void>("DELETE", `/api/auth/staff/${userId}`),
    },

    restaurants: {
      register: (request: RegisterRestaurantRequest) => http<Restaurant>("POST", "/api/restaurants", { body: request }),
      current: () => http<Restaurant>("GET", "/api/restaurants/current"),
      summary: (signal?: AbortSignal) => http<RestaurantSummary>("GET", "/api/restaurants/current/summary", { signal }),
      plans: () => http<SubscriptionPlan[]>("GET", "/api/restaurants/plans"),
      addBranch: (request: AddBranchRequest) => http<Branch>("POST", "/api/restaurants/current/branches", { body: request }),
      updateProfile: (request: UpdateRestaurantProfileRequest) => http<Restaurant>("PUT", "/api/restaurants/current/profile", { body: request }),
      setListing: (listed: boolean) => http<Restaurant>("PUT", "/api/restaurants/current/listing", { body: { listed } }),
      updateBranch: (branchId: Guid, request: UpdateBranchRequest) =>
        http<Branch>("PUT", `/api/restaurants/current/branches/${branchId}`, { body: request }),
      aiSettings: () => http<AiSettings>("GET", "/api/restaurants/current/ai-settings"),
      updateAiSettings: (knowledge: string, tone: string) =>
        http<AiSettings>("PUT", "/api/restaurants/current/ai-settings", { body: { knowledge, tone } }),
    },

    tables: {
      list: (branchId: Guid, signal?: AbortSignal) =>
        http<DiningTable[]>("GET", `/api/restaurants/current/branches/${branchId}/tables`, { signal }),
      create: (branchId: Guid, label: string, capacity: number) =>
        http<DiningTable>("POST", `/api/restaurants/current/branches/${branchId}/tables`, { body: { label, capacity } }),
      update: (tableId: Guid, label: string, capacity: number) =>
        http<DiningTable>("PUT", `/api/restaurants/current/tables/${tableId}`, { body: { label, capacity } }),
      remove: (tableId: Guid) => http<void>("DELETE", `/api/restaurants/current/tables/${tableId}`),
      rotateQr: (tableId: Guid) => http<DiningTable>("POST", `/api/restaurants/current/tables/${tableId}/rotate-qr`),
      setStatus: (tableId: Guid, status: TableStatus) =>
        http<DiningTable>("PUT", `/api/restaurants/current/tables/${tableId}/status`, { body: { status } }),
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
          query: { q: search.q, diets: search.diets, maxPrice: search.maxPrice, branchIds: search.branchIds, limit: search.limit },
          signal,
        }),
      resolveQr: (token: string, signal?: AbortSignal) =>
        http<QrVenue>("GET", `/api/discovery/restaurants/qr/${encodeURIComponent(token)}`, { signal }),
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
      update: (itemId: Guid, request: UpdateMenuItemRequest) => http<MenuItem>("PUT", `/api/menu/items/${itemId}`, { body: request }),
      remove: (itemId: Guid) => http<void>("DELETE", `/api/menu/items/${itemId}`),
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
      /** Staff board: a branch's orders, newest first. */
      list: (query: { branchId?: Guid; status?: OrderStatus[]; since?: string; take?: number }, signal?: AbortSignal) =>
        http<Order[]>("GET", "/api/orders", { query: { ...query, status: query.status }, signal }),
      mine: (signal?: AbortSignal) => http<Order[]>("GET", "/api/orders/mine", { signal }),
      setStatus: (orderId: Guid, status: OrderStatus, reason?: string) =>
        http<Order>("POST", `/api/orders/${orderId}/status`, { body: { status, reason } }),
      stats: (query: { from?: string; to?: string; branchId?: Guid } = {}, signal?: AbortSignal) =>
        http<OrderStats>("GET", "/api/orders/stats", { query, signal }),
    },

    serviceRequests: {
      open: (kind: ServiceRequestKind, note?: string, branchId?: Guid) =>
        http<ServiceRequest>("POST", "/api/service-requests", { body: { kind, note, branchId } }),
      mine: (signal?: AbortSignal) => http<ServiceRequest[]>("GET", "/api/service-requests/mine", { signal }),
      list: (branchId?: Guid, signal?: AbortSignal) => http<ServiceRequest[]>("GET", "/api/service-requests", { query: { branchId }, signal }),
      acknowledge: (id: Guid) => http<ServiceRequest>("POST", `/api/service-requests/${id}/acknowledge`),
      resolve: (id: Guid) => http<ServiceRequest>("POST", `/api/service-requests/${id}/resolve`),
    },

    reservations: {
      create: (request: CreateReservationRequest, idempotencyKey: string) =>
        http<Reservation>("POST", "/api/reservations", { body: request, idempotencyKey }),
      get: (reservationId: Guid, signal?: AbortSignal) =>
        http<Reservation>("GET", `/api/reservations/${reservationId}`, { signal }),
      cancel: (reservationId: Guid, reason: string, idempotencyKey?: string) =>
        http<Reservation>("POST", `/api/reservations/${reservationId}/cancel`, { body: { reason }, idempotencyKey }),
      list: (query: { branchId?: Guid; from?: string; to?: string }, signal?: AbortSignal) =>
        http<Reservation[]>("GET", "/api/reservations", { query, signal }),
      mine: (signal?: AbortSignal) => http<Reservation[]>("GET", "/api/reservations/mine", { signal }),
      seat: (id: Guid) => http<Reservation>("POST", `/api/reservations/${id}/seat`),
      complete: (id: Guid) => http<Reservation>("POST", `/api/reservations/${id}/complete`),
      noShow: (id: Guid) => http<Reservation>("POST", `/api/reservations/${id}/no-show`),
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
      /** One turn with the platform discovery assistant (restaurants and dishes across the platform). */
      discoveryTurn: (request: DiscoveryTurnRequest, signal?: AbortSignal) =>
        http<DiscoveryTurnResponse>("POST", "/api/ai/discovery/turns", { body: request, signal }),
      usage: (signal?: AbortSignal) => http<RestaurantAiUsage>("GET", "/api/ai/usage/current", { signal }),
    },

    admin: {
      overview: () => http<AdminOverview>("GET", "/api/admin/overview"),
      audit: (query: AuditQuery = {}, signal?: AbortSignal) => http<AuditEvent[]>("GET", "/api/admin/audit", { query: { ...query }, signal }),
      restaurants: (query: { status?: RestaurantStatus; search?: string } = {}, signal?: AbortSignal) =>
        http<AdminRestaurantSummary[]>("GET", "/api/admin/restaurants", { query, signal }),
      restaurantStats: () => http<AdminRestaurantStats>("GET", "/api/admin/restaurants/stats"),
      restaurant: (id: Guid) => http<Restaurant>("GET", `/api/admin/restaurants/${id}`),
      approve: (id: Guid) => http<Restaurant>("POST", `/api/admin/restaurants/${id}/approve`),
      suspend: (id: Guid, reason: string) => http<Restaurant>("POST", `/api/admin/restaurants/${id}/suspend`, { body: { reason } }),
      reinstate: (id: Guid) => http<Restaurant>("POST", `/api/admin/restaurants/${id}/reinstate`),
      changePlan: (id: Guid, planCode: string) => http<Restaurant>("PUT", `/api/admin/restaurants/${id}/plan`, { body: { planCode } }),
      setAi: (id: Guid, enabled: boolean) => http<Restaurant>("PUT", `/api/admin/restaurants/${id}/ai`, { body: { enabled } }),
      users: (search?: string) => http<AdminUser[]>("GET", "/api/admin/identity/users", { query: { search } }),
      tenantMembers: (tenantId: Guid) => http<StaffMember[]>("GET", `/api/admin/identity/tenants/${tenantId}/members`),
      createOwner: (request: { tenantId: Guid; tenantName: string; email: string; displayName?: string; password?: string }) =>
        http<AdminUser>("POST", "/api/admin/identity/owners", { body: request }),
      setUserActive: (userId: Guid, active: boolean) =>
        http<AdminUser>("POST", `/api/admin/identity/users/${userId}/${active ? "activate" : "deactivate"}`),
      aiUsage: () => http<PlatformAiUsage>("GET", "/api/admin/ai/usage"),
      agents: () => http<AgentRegistry>("GET", "/api/admin/ai/agents"),
    },
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
