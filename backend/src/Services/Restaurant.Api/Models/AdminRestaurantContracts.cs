namespace RestaurantAI.Restaurant.Api.Models;

public sealed record AdminRestaurantSummary(
    Guid Id,
    string Name,
    string Status,
    string PlanCode,
    bool AiEnabled,
    int BranchCount,
    string DefaultCurrency,
    DateTimeOffset CreatedAt);

public sealed record SuspendRequest(string Reason);

public sealed record ChangePlanRequest(string PlanCode);

public sealed record SetAiRequest(bool Enabled);
