using Microsoft.EntityFrameworkCore;
using RestaurantAI.Restaurant.Api.Persistence;
using RestaurantAI.Restaurant.Domain;
using RestaurantAI.SharedKernel.Tenancy;
using static RestaurantAI.Restaurant.Api.Endpoints.RestaurantEndpoints;

namespace RestaurantAI.Restaurant.Api.Endpoints;

/// <summary>
/// Platform administration of the restaurant lifecycle: verification, suspension, plans and AI
/// governance. Runs under the separate platform-administrator policy and sees every tenant.
/// </summary>
public static class AdminRestaurantEndpoints
{
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

    public static IEndpointRouteBuilder MapAdminRestaurantEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/restaurants")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(PlatformRoles.PlatformAdministrator));

        admin.MapGet("/", ListAsync);
        admin.MapGet("/stats", StatsAsync);
        admin.MapGet("/{restaurantId:guid}", GetAsync);
        admin.MapPost("/{restaurantId:guid}/approve", (Guid restaurantId, RestaurantDbContext db, TimeProvider time, CancellationToken ct) =>
            MutateAsync(restaurantId, db, time, ct, r => r.Approve()));
        admin.MapPost("/{restaurantId:guid}/suspend", (Guid restaurantId, SuspendRequest request, RestaurantDbContext db, TimeProvider time, CancellationToken ct) =>
            MutateAsync(restaurantId, db, time, ct, r => r.Suspend(request.Reason)));
        admin.MapPost("/{restaurantId:guid}/reinstate", (Guid restaurantId, RestaurantDbContext db, TimeProvider time, CancellationToken ct) =>
            MutateAsync(restaurantId, db, time, ct, r => r.Reinstate()));
        admin.MapPut("/{restaurantId:guid}/plan", (Guid restaurantId, ChangePlanRequest request, RestaurantDbContext db, TimeProvider time, CancellationToken ct) =>
            MutateAsync(restaurantId, db, time, ct, r => r.ChangePlan(request.PlanCode)));
        admin.MapPut("/{restaurantId:guid}/ai", (Guid restaurantId, SetAiRequest request, RestaurantDbContext db, TimeProvider time, CancellationToken ct) =>
            MutateAsync(restaurantId, db, time, ct, r => r.SetAiEnabled(request.Enabled)));

        return app;
    }

    private static async Task<IResult> ListAsync(string? status, string? search, RestaurantDbContext dbContext, CancellationToken cancellationToken)
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

    private static async Task<IResult> StatsAsync(RestaurantDbContext dbContext, CancellationToken cancellationToken)
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

    private static async Task<IResult> GetAsync(Guid restaurantId, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        var restaurant = await dbContext.Restaurants.AsNoTracking().Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == restaurantId, cancellationToken);
        return restaurant is null ? TypedResults.NotFound() : TypedResults.Ok(RestaurantResponse.From(restaurant));
    }

    private static async Task<IResult> MutateAsync(
        Guid restaurantId,
        RestaurantDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        Action<Domain.Restaurant> change)
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
