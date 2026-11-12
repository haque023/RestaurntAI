using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Services;

/// <summary>
/// Platform administration of the restaurant lifecycle: verification, suspension, plans and AI
/// governance. Runs under the separate platform-administrator policy and sees every tenant.
/// </summary>
public sealed class AdminRestaurantService(RestaurantDbContext dbContext, TimeProvider timeProvider) : IAdminRestaurantService
{
    public async Task<IResult> ListAsync(string? status, string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Restaurants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RestaurantStatus>(status, ignoreCase: true, out var parsed))
            {
                return TypedResults.Problem(title: "admin.invalid_status", detail: $"Status must be one of: {string.Join(", ", Enum.GetNames<RestaurantStatus>())}.", statusCode: StatusCodes.Status400BadRequest);
            }

            query = query.Where(r => r.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(term));
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AdminRestaurantSummary(r.Id, r.Name, r.Status.ToString(), r.PlanCode, r.AiEnabled, r.Branches.Count, r.DefaultCurrency, r.CreatedAt))
            .Take(500)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows);
    }

    public async Task<IResult> StatsAsync(CancellationToken cancellationToken)
    {
        var byStatus = await dbContext.Restaurants.AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byPlan = await dbContext.Restaurants.AsNoTracking()
            .GroupBy(r => r.PlanCode)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new
        {
            restaurants = byStatus.Sum(s => s.Count),
            byStatus = byStatus.ToDictionary(s => s.Status.ToString(), s => s.Count),
            byPlan = byPlan.ToDictionary(p => p.Plan, p => p.Count),
            branches = await dbContext.Set<Branch>().CountAsync(cancellationToken),
            listedBranches = await dbContext.DiscoveryListings.CountAsync(cancellationToken),
            tables = await dbContext.Tables.CountAsync(cancellationToken),
            monthlyRecurringRevenue = byPlan.Sum(p => (SubscriptionPlans.Find(p.Plan)?.MonthlyPrice ?? 0) * p.Count),
        });
    }

    public async Task<IResult> GetAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var restaurant = await dbContext.Restaurants.AsNoTracking().Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == restaurantId, cancellationToken);
        return restaurant is null ? TypedResults.NotFound() : TypedResults.Ok(RestaurantResponse.From(restaurant));
    }

    public Task<IResult> ApproveAsync(Guid restaurantId, CancellationToken cancellationToken) =>
        MutateAsync(restaurantId, cancellationToken, r => r.Approve());

    public Task<IResult> SuspendAsync(Guid restaurantId, SuspendRequest request, CancellationToken cancellationToken) =>
        MutateAsync(restaurantId, cancellationToken, r => r.Suspend(request.Reason));

    public Task<IResult> ReinstateAsync(Guid restaurantId, CancellationToken cancellationToken) =>
        MutateAsync(restaurantId, cancellationToken, r => r.Reinstate());

    public Task<IResult> ChangePlanAsync(Guid restaurantId, ChangePlanRequest request, CancellationToken cancellationToken) =>
        MutateAsync(restaurantId, cancellationToken, r => r.ChangePlan(request.PlanCode));

    public Task<IResult> SetAiAsync(Guid restaurantId, SetAiRequest request, CancellationToken cancellationToken) =>
        MutateAsync(restaurantId, cancellationToken, r => r.SetAiEnabled(request.Enabled));

    private async Task<IResult> MutateAsync(Guid restaurantId, CancellationToken cancellationToken, Action<Models.Restaurant> change)
    {
        var restaurant = await dbContext.Restaurants.Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == restaurantId, cancellationToken);
        if (restaurant is null)
        {
            return TypedResults.NotFound();
        }

        change(restaurant);
        await dbContext.SyncDiscoveryListingsAsync(restaurant, timeProvider, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(RestaurantResponse.From(restaurant));
    }
}
