using Microsoft.EntityFrameworkCore;
using RestaurantAI.Restaurant.Api.Persistence;
using RestaurantAI.Restaurant.Domain;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Endpoints;

public static class RestaurantEndpoints
{
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
        public static RestaurantResponse From(Domain.Restaurant r) => new(
            r.Id, r.Name, r.Description, r.Cuisines, r.DietaryOptions, r.DefaultCurrency, r.TimeZone, r.Status.ToString(), r.IsListed,
            r.Branches.OrderBy(b => b.Name).Select(BranchResponse.From).ToList(),
            r.Phone, r.Website, PlanResponse.From(r.Plan), r.AiEnabled, r.SuspensionReason, r.CreatedAt);
    }

    public static IEndpointRouteBuilder MapRestaurantEndpoints(this IEndpointRouteBuilder app)
    {
        var restaurants = app.MapGroup("/api/restaurants").WithTags("Restaurants");

        // Platform onboarding creates the tenant; the resulting id becomes the tenant id.
        restaurants.MapPost("/", RegisterAsync).RequireAuthorization(policy => policy.RequireRole(PlatformRoles.PlatformAdministrator));
        restaurants.MapGet("/current", GetCurrentAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapGet("/current/summary", GetSummaryAsync).RequirePermission(PlatformPermissions.MenuRead);
        restaurants.MapPut("/current/profile", UpdateProfileAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapPut("/current/listing", SetListingAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapPost("/current/branches", AddBranchAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapPut("/current/branches/{branchId:guid}", UpdateBranchAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapGet("/current/ai-settings", GetAiSettingsAsync).RequirePermission(PlatformPermissions.AiManage);
        restaurants.MapPut("/current/ai-settings", UpdateAiSettingsAsync).RequirePermission(PlatformPermissions.AiManage);
        restaurants.MapGet("/plans", () => TypedResults.Ok(SubscriptionPlans.All.Select(PlanResponse.From))).RequireAuthorization();

        // Any caller scoped to the restaurant (including guests); used by the AI platform with the caller's token.
        restaurants.MapGet("/current/ai-context", GetAiContextAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegisterRestaurantRequest request, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.DefaultCurrency.Length != 3)
        {
            return TypedResults.Problem(title: "restaurant.invalid", detail: "Name and a 3-letter currency are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!IsKnownTimeZone(request.TimeZone))
        {
            return TypedResults.Problem(title: "restaurant.invalid_time_zone", detail: "Use an IANA time zone such as Asia/Dhaka or Europe/London.", statusCode: StatusCodes.Status400BadRequest);
        }

        var restaurant = Domain.Restaurant.Register(
            request.Name.Trim(), request.DefaultCurrency.ToUpperInvariant(), request.TimeZone, request.SelfService, timeProvider.GetUtcNow());
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/restaurants/{restaurant.Id}", RestaurantResponse.From(restaurant));
    }

    private static async Task<IResult> GetCurrentAsync(ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        var restaurant = await dbContext.Restaurants.AsNoTracking().Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        return restaurant is null ? TypedResults.NotFound() : TypedResults.Ok(RestaurantResponse.From(restaurant));
    }

    private static Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            restaurant.UpdateProfile(request.Description, request.Cuisines, request.DietaryOptions);
            restaurant.UpdateContact(request.Phone, request.Website);
            return TypedResults.Ok(RestaurantResponse.From(restaurant));
        });

    private static Task<IResult> SetListingAsync(SetListingRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            if (request.Listed)
            {
                restaurant.Activate();
            }
            else
            {
                restaurant.Unlist();
            }

            return TypedResults.Ok(RestaurantResponse.From(restaurant));
        });

    private static Task<IResult> AddBranchAsync(AddBranchRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            var branch = restaurant.AddBranch(request.Name, request.Address);
            return TypedResults.Created($"/api/restaurants/current/branches/{branch.Id}", BranchResponse.From(branch));
        });

    private static Task<IResult> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            var branch = restaurant.GetBranch(branchId);
            branch.UpdateDetails(request.Name, request.Address, request.Latitude, request.Longitude);
            branch.SetOpeningHours(request.OpeningHours.Select(p => new OpeningPeriod(p.Day, p.Opens, p.Closes)));
            return TypedResults.Ok(BranchResponse.From(branch));
        });

    public sealed record RestaurantSummary(Guid Id, string Name, string Status, bool IsListed, string DefaultCurrency, string TimeZone, IReadOnlyList<BranchResponse> Branches);

    /// <summary>What every staff member needs to work: the restaurant's name, status and branches.</summary>
    private static async Task<IResult> GetSummaryAsync(ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        var r = await dbContext.Restaurants.AsNoTracking().Include(x => x.Branches).SingleOrDefaultAsync(x => x.Id == tenant.TenantId, cancellationToken);
        if (r is null)
        {
            return TypedResults.NotFound();
        }

        var branches = r.Branches.Where(b => tenant.BranchId is null || b.Id == tenant.BranchId).OrderBy(b => b.Name).Select(BranchResponse.From).ToList();
        return TypedResults.Ok(new RestaurantSummary(r.Id, r.Name, r.Status.ToString(), r.IsListed, r.DefaultCurrency, r.TimeZone, branches));
    }

    private static async Task<IResult> GetAiSettingsAsync(ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        var restaurant = await dbContext.Restaurants.AsNoTracking().SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        return restaurant is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new AiSettingsResponse(restaurant.AiEnabled, restaurant.AiKnowledge, restaurant.AiTone, Domain.Restaurant.MaxAiKnowledgeLength));
    }

    private static Task<IResult> UpdateAiSettingsAsync(AiSettingsRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            restaurant.UpdateAiSettings(request.Knowledge ?? string.Empty, request.Tone ?? string.Empty);
            return TypedResults.Ok(new AiSettingsResponse(restaurant.AiEnabled, restaurant.AiKnowledge, restaurant.AiTone, Domain.Restaurant.MaxAiKnowledgeLength));
        });

    private static async Task<IResult> GetAiContextAsync(ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return TypedResults.Problem(title: "tenant.required", detail: "A tenant context is required.", statusCode: StatusCodes.Status403Forbidden);
        }

        var restaurant = await dbContext.Restaurants.AsNoTracking().Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == tenantId, cancellationToken);
        if (restaurant is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new AiContextResponse(
            restaurant.Id,
            restaurant.Name,
            restaurant.Status.ToString(),
            restaurant.AiEnabled && restaurant.Plan.AiWaiter && restaurant.Status != RestaurantStatus.Suspended,
            restaurant.Plan.AiTurnsPerMonth,
            restaurant.AiKnowledge,
            restaurant.AiTone,
            restaurant.DefaultCurrency,
            restaurant.TimeZone,
            restaurant.Phone,
            restaurant.Website,
            restaurant.Branches.Select(BranchResponse.From).ToList()));
    }

    internal static bool IsKnownTimeZone(string? timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _);

    /// <summary>Loads the caller's restaurant, applies a change and commits it with the discovery read model.</summary>
    private static async Task<IResult> MutateAsync(
        ITenantContext tenant,
        RestaurantDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        Func<Domain.Restaurant, IResult> change)
    {
        var restaurant = await dbContext.Restaurants.Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        if (restaurant is null)
        {
            return TypedResults.NotFound();
        }

        var result = change(restaurant);
        await dbContext.SyncDiscoveryListingsAsync(restaurant, timeProvider, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}
