using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Services;

public sealed class RestaurantService(RestaurantDbContext dbContext, TimeProvider timeProvider, ITenantContext tenant) : IRestaurantService
{
    public IResult GetPlans() => TypedResults.Ok(SubscriptionPlans.All.Select(PlanResponse.From));

    /// <summary>Platform onboarding creates the tenant; the resulting id becomes the tenant id.</summary>
    public async Task<IResult> RegisterAsync(RegisterRestaurantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.DefaultCurrency.Length != 3)
        {
            return TypedResults.Problem(title: "restaurant.invalid", detail: "Name and a 3-letter currency are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!IsKnownTimeZone(request.TimeZone))
        {
            return TypedResults.Problem(title: "restaurant.invalid_time_zone", detail: "Use an IANA time zone such as Asia/Dhaka or Europe/London.", statusCode: StatusCodes.Status400BadRequest);
        }

        var restaurant = Models.Restaurant.Register(
            request.Name.Trim(), request.DefaultCurrency.ToUpperInvariant(), request.TimeZone, request.SelfService, timeProvider.GetUtcNow());
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/restaurants/{restaurant.Id}", RestaurantResponse.From(restaurant));
    }

    public async Task<IResult> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var restaurant = await dbContext.Restaurants.AsNoTracking().Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        return restaurant is null ? TypedResults.NotFound() : TypedResults.Ok(RestaurantResponse.From(restaurant));
    }

    public Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        MutateAsync(cancellationToken, restaurant =>
        {
            restaurant.UpdateProfile(request.Description, request.Cuisines, request.DietaryOptions);
            restaurant.UpdateContact(request.Phone, request.Website);
            return TypedResults.Ok(RestaurantResponse.From(restaurant));
        });

    public Task<IResult> SetListingAsync(SetListingRequest request, CancellationToken cancellationToken) =>
        MutateAsync(cancellationToken, restaurant =>
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

    public Task<IResult> AddBranchAsync(AddBranchRequest request, CancellationToken cancellationToken) =>
        MutateAsync(cancellationToken, restaurant =>
        {
            var branch = restaurant.AddBranch(request.Name, request.Address);
            return TypedResults.Created($"/api/restaurants/current/branches/{branch.Id}", BranchResponse.From(branch));
        });

    public Task<IResult> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken) =>
        MutateAsync(cancellationToken, restaurant =>
        {
            var branch = restaurant.GetBranch(branchId);
            branch.UpdateDetails(request.Name, request.Address, request.Latitude, request.Longitude);
            branch.SetOpeningHours(request.OpeningHours.Select(p => new OpeningPeriod(p.Day, p.Opens, p.Closes)));
            return TypedResults.Ok(BranchResponse.From(branch));
        });

    /// <summary>What every staff member needs to work: the restaurant's name, status and branches.</summary>
    public async Task<IResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var r = await dbContext.Restaurants.AsNoTracking().Include(x => x.Branches).SingleOrDefaultAsync(x => x.Id == tenant.TenantId, cancellationToken);
        if (r is null)
        {
            return TypedResults.NotFound();
        }

        var branches = r.Branches.Where(b => tenant.BranchId is null || b.Id == tenant.BranchId).OrderBy(b => b.Name).Select(BranchResponse.From).ToList();
        return TypedResults.Ok(new RestaurantSummary(r.Id, r.Name, r.Status.ToString(), r.IsListed, r.DefaultCurrency, r.TimeZone, branches));
    }

    public async Task<IResult> GetAiSettingsAsync(CancellationToken cancellationToken)
    {
        var restaurant = await dbContext.Restaurants.AsNoTracking().SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        return restaurant is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new AiSettingsResponse(restaurant.AiEnabled, restaurant.AiKnowledge, restaurant.AiTone, Models.Restaurant.MaxAiKnowledgeLength));
    }

    public Task<IResult> UpdateAiSettingsAsync(AiSettingsRequest request, CancellationToken cancellationToken) =>
        MutateAsync(cancellationToken, restaurant =>
        {
            restaurant.UpdateAiSettings(request.Knowledge ?? string.Empty, request.Tone ?? string.Empty);
            return TypedResults.Ok(new AiSettingsResponse(restaurant.AiEnabled, restaurant.AiKnowledge, restaurant.AiTone, Models.Restaurant.MaxAiKnowledgeLength));
        });

    /// <summary>Any caller scoped to the restaurant (including guests); used by the AI platform with the caller's token.</summary>
    public async Task<IResult> GetAiContextAsync(CancellationToken cancellationToken)
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
    private async Task<IResult> MutateAsync(CancellationToken cancellationToken, Func<Models.Restaurant, IResult> change)
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
