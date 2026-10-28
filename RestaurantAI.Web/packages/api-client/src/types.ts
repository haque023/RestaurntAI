// Mirrors the backend contracts (RestaurantAI/src/Application/*, src/Api/*/Endpoints).
// Keep in sync when API contracts change.

export type Guid = string;
export type IsoDateTime = string;

export const PlatformRoles = {
  PlatformAdministrator: "platform_admin",
  RestaurantOwner: "restaurant_owner",
  RestaurantManager: "restaurant_manager",
  RestaurantStaff: "restaurant_staff",
  KitchenStaff: "kitchen_staff",
  Cashier: "cashier",
  Waiter: "waiter",
  MarketingUser: "marketing_user",
  Customer: "customer",
} as const;

export const Permissions = {
  MenuRead: "menu.read",
  MenuWrite: "menu.write",
  OrdersCreate: "orders.create",
  OrdersRead: "orders.read",
  OrdersCancel: "orders.cancel",
  OrdersManage: "orders.manage",
  ReservationsCreate: "reservations.create",
  ReservationsRead: "reservations.read",
  ReservationsCancel: "reservations.cancel",
  ReservationsManage: "reservations.manage",
  LoyaltyRead: "loyalty.read",
  LoyaltyManage: "loyalty.manage",
  RestaurantManage: "restaurant.manage",
  ConsumerProfile: "consumer.profile",
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];

/** RFC 9457 problem details as returned by every service. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  correlationId?: string;
  [key: string]: unknown;
}

// ---- Identity
export interface DevTokenRequest {
  userId?: Guid;
  tenantId?: Guid;
  branchId?: Guid;
  roles: string[];
  permissions: string[];
  lifetimeMinutes?: number;
}

export interface DevTokenResponse {
  access_token: string;
  token_type: "Bearer";
}

// ---- Restaurant
export type DayOfWeek = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

/** Weekly opening period in the restaurant's local time ("HH:mm:ss"). Closes < opens = past midnight. */
export interface OpeningPeriod {
  day: DayOfWeek;
  opens: string;
  closes: string;
}

export interface Branch {
  id: Guid;
  name: string;
  address: string;
  latitude: number | null;
  longitude: number | null;
  openingHours: OpeningPeriod[];
}

export interface UpdateBranchRequest {
  name: string;
  address: string;
  latitude: number | null;
  longitude: number | null;
  openingHours: OpeningPeriod[];
}

export interface UpdateRestaurantProfileRequest {
  description: string;
  cuisines: string[];
  dietaryOptions: string[];
}

export type RestaurantStatus = "Draft" | "PendingVerification" | "Active" | "Suspended";

export interface Restaurant {
  id: Guid;
  name: string;
  description: string;
  cuisines: string[];
  dietaryOptions: string[];
  defaultCurrency: string;
  timeZone: string;
  status: RestaurantStatus;
  /** Listed restaurants appear in consumer discovery. */
  isListed: boolean;
  branches: Branch[];
}

export interface RegisterRestaurantRequest {
  name: string;
  defaultCurrency: string;
  timeZone: string;
}

export interface AddBranchRequest {
  name: string;
  address: string;
}

// ---- Discovery (public, no sign-in)
export interface DiscoverySearch {
  q?: string;
  cuisines?: string[];
  diets?: string[];
  lat?: number;
  lng?: number;
  radiusKm?: number;
  openNow?: boolean;
  branchIds?: Guid[];
  tenantIds?: Guid[];
  limit?: number;
}

/** One listed branch of a restaurant. */
export interface DiscoveryBranch {
  tenantId: Guid;
  branchId: Guid;
  restaurantName: string;
  description: string;
  cuisines: string[];
  dietaryOptions: string[];
  branchName: string;
  address: string;
  latitude: number | null;
  longitude: number | null;
  distanceKm: number | null;
  isOpenNow: boolean;
  openingHours: OpeningPeriod[];
}

export interface DishSearch {
  q?: string;
  diets?: string[];
  maxPrice?: number;
  limit?: number;
}

export interface DishResult {
  menuItemId: Guid;
  tenantId: Guid;
  branchId: Guid;
  name: string;
  category: string;
  description: string;
  dietaryTags: string[];
  price: number;
  currency: string;
}

// ---- Menu
export interface MenuItem {
  id: Guid;
  branchId: Guid;
  category: string;
  name: string;
  description: string;
  price: number;
  currency: string;
  isAvailable: boolean;
  dietaryTags: string[];
  ingredients: string[];
}

export interface MenuSearch {
  q?: string;
  maxPrice?: number;
  dietary?: string[];
  limit?: number;
}

export interface CreateMenuItemRequest {
  category: string;
  name: string;
  description: string;
  price: number;
  currency: string;
  dietaryTags: string[];
  ingredients: string[];
}

export interface MenuAvailability {
  menuItemId: Guid;
  isAvailable: boolean;
  price: number;
  currency: string;
  checkedAt: IsoDateTime;
}

// ---- Orders
export type OrderChannel = "QrTable" | "ConsumerApp" | "ConsumerWeb" | "Staff" | "AiWaiter";
export type OrderStatus = "Placed" | "Accepted" | "Preparing" | "Ready" | "Served" | "Completed" | "Cancelled";

export interface PlaceOrderRequest {
  branchId: Guid;
  tableId?: Guid | null;
  channel: OrderChannel;
  items: { menuItemId: Guid; quantity: number; notes?: string | null }[];
  notes?: string | null;
}

export interface OrderLine {
  menuItemId: Guid;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  notes: string | null;
}

export interface Order {
  id: Guid;
  branchId: Guid;
  tableId: Guid | null;
  status: OrderStatus;
  total: number;
  currency: string;
  placedAt: IsoDateTime;
  lines: OrderLine[];
}

// ---- Reservations
export type ReservationStatus = "Pending" | "Confirmed" | "Seated" | "Completed" | "Cancelled" | "NoShow";

export interface CreateReservationRequest {
  branchId: Guid;
  tableId?: Guid | null;
  partySize: number;
  startsAt: IsoDateTime;
  durationMinutes?: number | null;
  specialRequests?: string | null;
}

export interface Reservation {
  id: Guid;
  branchId: Guid;
  tableId: Guid | null;
  partySize: number;
  startsAt: IsoDateTime;
  endsAt: IsoDateTime;
  status: ReservationStatus;
  specialRequests: string | null;
}

// ---- Consumer
export interface CustomerProfile {
  id: Guid;
  displayName: string;
  preferredLanguage: string | null;
  dietaryPreferences: string[];
  marketingConsent: boolean;
}

export interface UpdateProfileRequest {
  displayName: string;
  preferredLanguage: string | null;
  dietaryPreferences: string[];
  marketingConsent: boolean;
}

export interface FavoriteRestaurant {
  restaurantId: Guid;
  savedAt: IsoDateTime;
}

// ---- Loyalty
export interface LoyaltyBalance {
  accountId: Guid;
  customerId: Guid;
  balance: number;
}

// ---- Admin
export interface AdminOverview {
  environment: string;
  serverTime: IsoDateTime;
}

// ---- AI waiter
export interface WaiterTurnRequest {
  conversationId?: Guid | null;
  branchId: Guid;
  tableId?: Guid | null;
  message: string;
}

export type WaiterTurnOutcome = "Answered" | "Declined" | "Truncated";

export interface WaiterTurnResponse {
  conversationId: Guid;
  turnId: Guid;
  reply: string;
  outcome: WaiterTurnOutcome;
}
