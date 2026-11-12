namespace RestaurantAI.Restaurant.Api.Models;

public sealed record RegisterRestaurantRequest(string Name, string DefaultCurrency, string TimeZone, bool SelfService = false);

public sealed record UpdateProfileRequest(string Description, IReadOnlyList<string> Cuisines, IReadOnlyList<string> DietaryOptions, string? Phone = null, string? Website = null);

public sealed record AiSettingsRequest(string Knowledge, string Tone);

public sealed record AiSettingsResponse(bool Enabled, string Knowledge, string Tone, int MaxKnowledgeLength);

public sealed record PlanResponse(string Code, string Name, decimal MonthlyPrice, string Currency, int MaxBranches, int MaxTablesPerBranch, bool AiWaiter, int AiTurnsPerMonth)
{
    public static PlanResponse From(SubscriptionPlan p) =>
        new(p.Code, p.Name, p.MonthlyPrice, p.Currency, p.MaxBranches, p.MaxTablesPerBranch, p.AiWaiter, p.AiTurnsPerMonth);
}

/// <summary>What the AI platform needs to serve a restaurant: governance, entitlements and knowledge.</summary>
public sealed record AiContextResponse(
    Guid TenantId,
    string RestaurantName,
    string Status,
    bool AiEnabled,
    int AiTurnsPerMonth,
    string Knowledge,
    string Tone,
    string DefaultCurrency,
    string TimeZone,
    string? Phone,
    string? Website,
    IReadOnlyList<BranchResponse> Branches);

public sealed record SetListingRequest(bool Listed);

public sealed record AddBranchRequest(string Name, string Address);

public sealed record OpeningPeriodDto(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

public sealed record UpdateBranchRequest(string Name, string Address, double? Latitude, double? Longitude, IReadOnlyList<OpeningPeriodDto> OpeningHours);

public sealed record BranchResponse(Guid Id, string Name, string Address, double? Latitude, double? Longitude, IReadOnlyList<OpeningPeriodDto> OpeningHours)
{
    public static BranchResponse From(Branch b) =>
        new(b.Id, b.Name, b.Address, b.Latitude, b.Longitude, b.OpeningHours.Select(p => new OpeningPeriodDto(p.Day, p.Opens, p.Closes)).ToList());
}

public sealed record RestaurantResponse(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> Cuisines,
    IReadOnlyList<string> DietaryOptions,
    string DefaultCurrency,
    string TimeZone,
    string Status,
    bool IsListed,
    IReadOnlyList<BranchResponse> Branches,
    string? Phone,
    string? Website,
    PlanResponse Plan,
    bool AiEnabled,
    string? SuspensionReason,
    DateTimeOffset CreatedAt)
{
    public static RestaurantResponse From(Models.Restaurant r) => new(
        r.Id, r.Name, r.Description, r.Cuisines, r.DietaryOptions, r.DefaultCurrency, r.TimeZone, r.Status.ToString(), r.IsListed,
        r.Branches.OrderBy(b => b.Name).Select(BranchResponse.From).ToList(),
        r.Phone, r.Website, PlanResponse.From(r.Plan), r.AiEnabled, r.SuspensionReason, r.CreatedAt);
}

public sealed record RestaurantSummary(Guid Id, string Name, string Status, bool IsListed, string DefaultCurrency, string TimeZone, IReadOnlyList<BranchResponse> Branches);
