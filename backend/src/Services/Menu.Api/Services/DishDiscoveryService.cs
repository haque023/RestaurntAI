using Microsoft.EntityFrameworkCore;

namespace RestaurantAI.Menu.Api.Services;

/// <summary>Public dish search across restaurants ("where can I get ramen?").</summary>
public sealed class DishDiscoveryService(MenuDbContext dbContext) : IDishDiscoveryService
{
    public async Task<IResult> SearchAsync(string? q, string? diets, decimal? maxPrice, string? branchIds, int? limit, CancellationToken cancellationToken)
    {
        var branches = new List<Guid>();
        foreach (var part in (branchIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id) || branches.Count >= 200)
            {
                return TypedResults.Problem(title: "discovery.invalid_ids", detail: "branchIds must be comma-separated GUIDs (max 200).", statusCode: StatusCodes.Status400BadRequest);
            }

            branches.Add(id);
        }

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

        if (branches.Count > 0)
        {
            items = items.Where(p => branches.Contains(p.BranchId));
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
