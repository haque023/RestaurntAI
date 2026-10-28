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
  StaffManage: "staff.manage",
  AnalyticsRead: "analytics.read",
  AiManage: "ai.manage",
  ServiceRequest: "service.request",
  DiscoverySearch: "discovery.search",
} as const;

/** Roles a restaurant can give its staff (owners are created at onboarding). */
export const StaffRoles = [
  PlatformRoles.RestaurantManager,
  PlatformRoles.Waiter,
  PlatformRoles.KitchenStaff,
  PlatformRoles.Cashier,
  PlatformRoles.RestaurantStaff,
  PlatformRoles.MarketingUser,
] as const;

export const RoleLabels: Record<string, string> = {
  [PlatformRoles.PlatformAdministrator]: "Platform admin",
  [PlatformRoles.RestaurantOwner]: "Owner",
  [PlatformRoles.RestaurantManager]: "Manager",
  [PlatformRoles.RestaurantStaff]: "Staff",
  [PlatformRoles.KitchenStaff]: "Kitchen",
  [PlatformRoles.Cashier]: "Cashier",
  [PlatformRoles.Waiter]: "Waiter",
  [PlatformRoles.MarketingUser]: "Marketing",
  [PlatformRoles.Customer]: "Customer",
};

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

/** Which app is signing in; decides what the issued token allows. */
export type SessionScope = "Customer" | "Staff" | "Admin";

export interface LoginRequest {
  email: string;
  password: string;
  scope?: SessionScope;
  tenantId?: Guid;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName?: string;
}

export interface RestaurantSignupRequest {
  email: string;
  password: string;
  displayName?: string;
  restaurantName: string;
  currency: string;
  timeZone: string;
}

export interface VenueSessionRequest {
  tenantId?: Guid;
  branchId?: Guid;
  qrToken?: string;
}

export interface Membership {
  tenantId: Guid;
  tenantName: string;
  branchId: Guid | null;
  role: string;
}

export interface SessionUser {
  id: Guid;
  email: string | null;
  displayName: string | null;
  isPlatformAdministrator: boolean;
  isGuest: boolean;
}

export interface SessionVenue {
  tenantId: Guid;
  branchId: Guid | null;
  tableId: Guid | null;
  restaurantName: string;
  branchName: string | null;
  tableLabel: string | null;
}

export interface SessionResponse {
  accessToken: string;
  tokenType: "Bearer";
  expiresIn: number;
  user: SessionUser;
  memberships: Membership[];
  venue?: SessionVenue | null;
}

export interface StaffMember {
  userId: Guid;
  email: string;
  displayName: string;
  isActive: boolean;
  lastLoginAt: IsoDateTime | null;
  roles: Membership[];
}

export interface AddStaffRequest {
  email: string;
  displayName?: string;
  password?: string;
  role: string;
  branchId?: Guid | null;
}

export interface AdminUser {
  id: Guid;
  email: string;
  displayName: string;
  isPlatformAdministrator: boolean;
  isActive: boolean;
  createdAt: IsoDateTime;
  lastLoginAt: IsoDateTime | null;
  memberships: Membership[];
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
  phone?: string | null;
  website?: string | null;
}

export interface SubscriptionPlan {
  code: string;
  name: string;
  monthlyPrice: number;
  currency: string;
  maxBranches: number;
  maxTablesPerBranch: number;
  aiWaiter: boolean;
  aiTurnsPerMonth: number;
}

export interface AiSettings {
  enabled: boolean;
  knowledge: string;
  tone: string;
  maxKnowledgeLength: number;
}

export type TableStatus = "Available" | "Occupied" | "Reserved" | "OutOfService";

export interface DiningTable {
  id: Guid;
  branchId: Guid;
  label: string;
  capacity: number;
  status: TableStatus;
  qrToken: string;
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
  phone: string | null;
  website: string | null;
  plan: SubscriptionPlan;
  aiEnabled: boolean;
  suspensionReason: string | null;
  createdAt: IsoDateTime;
}

/** Readable by every staff member: name, status and the branches they may work in. */
export interface RestaurantSummary {
  id: Guid;
  name: string;
  status: RestaurantStatus;
  isListed: boolean;
  defaultCurrency: string;
  timeZone: string;
  branches: Branch[];
}

export interface RegisterRestaurantRequest {
  name: string;
  defaultCurrency: string;
  timeZone: string;
}

export interface AdminRestaurantSummary {
  id: Guid;
  name: string;
  status: RestaurantStatus;
  planCode: string;
  aiEnabled: boolean;
  branchCount: number;
  defaultCurrency: string;
  createdAt: IsoDateTime;
}

export interface AdminRestaurantStats {
  restaurants: number;
  byStatus: Record<string, number>;
  byPlan: Record<string, number>;
  branches: number;
  listedBranches: number;
  tables: number;
  monthlyRecurringRevenue: number;
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
  branchIds?: Guid[];
  limit?: number;
}

export interface QrVenue {
  tenantId: Guid;
  branchId: Guid;
  tableId: Guid;
  restaurantName: string;
  branchName: string;
  tableLabel: string;
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

export type UpdateMenuItemRequest = CreateMenuItemRequest;

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
export type OrderStatus = "Placed" | "Accepted" | "Preparing" | "Ready" | "Served" | "Completed" | "Cancelled" | "Rejected";

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
  channel: OrderChannel;
  notes: string | null;
  cancellationReason: string | null;
  nextStatus: OrderStatus | null;
  updatedAt: IsoDateTime;
  history: { status: OrderStatus; at: IsoDateTime }[];
}

export interface OrderStats {
  from: IsoDateTime;
  to: IsoDateTime;
  currency: string | null;
  orders: number;
  completed: number;
  cancelled: number;
  rejected: number;
  open: number;
  revenue: number;
  averageOrderValue: number;
  averagePrepMinutes: number | null;
  byChannel: Record<string, number>;
  byStatus: Record<string, number>;
  topItems: { menuItemId: Guid; name: string; quantity: number; revenue: number }[];
  daily: { date: string; orders: number; revenue: number }[];
}

export type ServiceRequestKind = "CallWaiter" | "RequestBill" | "Assistance" | "AiEscalation";

export interface ServiceRequest {
  id: Guid;
  branchId: Guid;
  tableId: Guid | null;
  kind: ServiceRequestKind;
  note: string;
  status: "Open" | "Acknowledged" | "Resolved";
  createdAt: IsoDateTime;
  resolvedAt: IsoDateTime | null;
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
  month: {
    from: IsoDateTime;
    orders: number;
    grossOrderValue: Record<string, number>;
    reservations: number;
    newRestaurants: number;
    newUsers: number;
    aiToolCalls: number;
  };
  last24h: Record<string, number>;
}

export interface AuditEvent {
  id: Guid;
  type: string;
  module: string;
  tenantId: Guid | null;
  actorId: Guid | null;
  correlationId: string | null;
  payload: Record<string, unknown>;
  occurredAt: IsoDateTime;
}

export interface AuditQuery {
  type?: string;
  module?: string;
  tenantId?: Guid;
  actorId?: Guid;
  take?: number;
}

export interface AiUsageSummary {
  turns: number;
  inputTokens: number;
  outputTokens: number;
  toolCalls: number;
  failed: number;
  declined: number;
  averageLatencyMs: number;
  byAgent: Record<string, number>;
  daily: { date: string; turns: number; tokens: number }[];
}

export interface RestaurantAiUsage {
  periodStart: IsoDateTime;
  limit: number | null;
  aiEnabled: boolean | null;
  usedTurns: number;
  summary: AiUsageSummary;
}

export interface PlatformAiUsage {
  from: IsoDateTime;
  to: IsoDateTime;
  summary: AiUsageSummary;
  byTenant: { tenantId: Guid | null; turns: number; tokens: number; failed: number }[];
}

export interface AgentRegistry {
  model: string;
  effort: string;
  maxOutputTokens: number;
  refusalFallback: boolean;
  agents: { name: string; description: string; tools: string[]; maxToolCallsPerTurn: number }[];
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

export interface DiscoveryTurnRequest {
  conversationId?: Guid | null;
  message: string;
  latitude?: number | null;
  longitude?: number | null;
}

export type DiscoveryTurnResponse = WaiterTurnResponse;
