namespace RestaurantAI.SharedKernel.Tenancy;

/// <summary>
/// Claim types carried in access tokens issued for the platform.
/// </summary>
public static class PlatformClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string BranchId = "branch_id";
    public const string TableId = "table_id";
    public const string Role = "role";
    public const string Permission = "permission";
}

public static class PlatformRoles
{
    public const string PlatformAdministrator = "platform_admin";
    public const string RestaurantOwner = "restaurant_owner";
    public const string RestaurantManager = "restaurant_manager";
    public const string RestaurantStaff = "restaurant_staff";
    public const string KitchenStaff = "kitchen_staff";
    public const string Cashier = "cashier";
    public const string Waiter = "waiter";
    public const string MarketingUser = "marketing_user";
    public const string Customer = "customer";
}

public static class PlatformHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string IdempotencyKey = "Idempotency-Key";
    public const string ConversationId = "X-Conversation-Id";
    public const string TurnId = "X-Turn-Id";
}
