using Microsoft.EntityFrameworkCore;
using RestaurantAI.Menu.Api.Persistence;

namespace RestaurantAI.Menu.Api.Endpoints;

/// <summary>Public dish search across restaurants ("where can I get ramen?").</summary>
public static class DishDiscoveryEndpoints
{
    public sealed record DishResult(Guid MenuItemId, Guid TenantId, Guid BranchId, string Name, string Category, string Description, IReadOnlyList<string> DietaryTags, decimal Price, string Currency);

    public static IEndpointRouteBuilder MapDishDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/discovery/menu-items", SearchAsync).WithTags("Discovery").AllowAnonymous();
        return app;
    }

    private static async Task<IResult> SearchAsync(string? q, string? diets, decimal? maxPrice, int? limit, MenuDbContext dbContext, CancellationToken cancellationToken)
    {
        var tags = (diets ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .Take(10)
            .ToList();

        if (string.IsNullOrWhiteSpace(q) && tags.Count == 0)
        {
            return TypedResults.Problem(title: "discovery.query_required", detail: "Provide a dish name (q) or dietary tags.", statusCode: StatusCodes.Status400BadRequest);
        }

        var items = dbContext.PublicMenuItems.AsNoTracking().Where(p => p.IsAvailable);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var escaped = q.Trim().ToLowerInvariant().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escaped}%";
            items = items.Where(p => EF.Functions.Like(p.SearchText, pattern));
        }

        foreach (var tag in tags)
        {
            items = items.Where(p => p.DietaryTags.Contains(tag));
        }

        if (maxPrice is { } max)
        {
            items = items.Where(p => p.Price <= max);
        }

        var results = await items
            .OrderBy(p => p.Price)
            .Take(Math.Clamp(limit ?? 50, 1, 200))
            .Select(p => new DishResult(p.MenuItemId, p.TenantId, p.BranchId, p.Name, p.Category, p.Description, p.DietaryTags, p.Price, p.Currency))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(results);
    }
}
