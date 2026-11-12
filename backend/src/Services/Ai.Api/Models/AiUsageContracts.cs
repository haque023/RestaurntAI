namespace RestaurantAI.AI.Api.Models;

public sealed record UsageSummary(
    int Turns,
    long InputTokens,
    long OutputTokens,
    int ToolCalls,
    int Failed,
    int Declined,
    double AverageLatencyMs,
    IReadOnlyDictionary<string, int> ByAgent,
    IReadOnlyList<DailyUsage> Daily);

public sealed record DailyUsage(DateOnly Date, int Turns, long Tokens);

public sealed record TenantUsage(Guid? TenantId, int Turns, long Tokens, int Failed);
