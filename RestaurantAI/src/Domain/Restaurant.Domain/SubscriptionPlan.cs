namespace RestaurantAI.Restaurant.Domain;

/// <summary>
/// A subscription plan and the entitlements it grants. Limits are enforced by the services
/// (branches here, AI turns in the AI platform); commercial terms stay out of AI prompts.
/// </summary>
public sealed record SubscriptionPlan(
    string Code,
    string Name,
    decimal MonthlyPrice,
    string Currency,
    int MaxBranches,
    int MaxTablesPerBranch,
    bool AiWaiter,
    int AiTurnsPerMonth);

public static class SubscriptionPlans
{
    public static readonly SubscriptionPlan Starter = new("starter", "Starter", 0m, "USD", MaxBranches: 1, MaxTablesPerBranch: 15, AiWaiter: true, AiTurnsPerMonth: 300);

    public static readonly SubscriptionPlan Growth = new("growth", "Growth", 49m, "USD", MaxBranches: 5, MaxTablesPerBranch: 60, AiWaiter: true, AiTurnsPerMonth: 3_000);

    public static readonly SubscriptionPlan Pro = new("pro", "Pro", 199m, "USD", MaxBranches: 50, MaxTablesPerBranch: 200, AiWaiter: true, AiTurnsPerMonth: 30_000);

    public static readonly IReadOnlyList<SubscriptionPlan> All = [Starter, Growth, Pro];

    public static SubscriptionPlan? Find(string? code) =>
        All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
}
