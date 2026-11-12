namespace RestaurantAI.Order.Api.Models;

public sealed record DailyPoint(DateOnly Date, int Orders, decimal Revenue);

public sealed record TopItem(Guid MenuItemId, string Name, int Quantity, decimal Revenue);

public sealed record OrderStats(
    DateTimeOffset From,
    DateTimeOffset To,
    string? Currency,
    int Orders,
    int Completed,
    int Cancelled,
    int Rejected,
    int Open,
    decimal Revenue,
    decimal AverageOrderValue,
    double? AveragePrepMinutes,
    IReadOnlyDictionary<string, int> ByChannel,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyList<TopItem> TopItems,
    IReadOnlyList<DailyPoint> Daily);
