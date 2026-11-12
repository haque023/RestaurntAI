using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Menu.Api.Services;

public sealed class MenuService(IMenuItemRepository items, TimeProvider timeProvider, MenuWorkflow workflow, MenuDbContext dbContext) : IMenuService
{
    public async Task<IResult> GetMenuAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var result = await items.ListForBranchAsync(branchId, cancellationToken);
        return TypedResults.Ok(result.Select(MenuItemResponse.From));
    }

    public async Task<IResult> SearchAsync(Guid branchId, string? q, decimal? maxPrice, string? dietary, int? limit, CancellationToken cancellationToken)
    {
        var tags = (dietary ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToList();

        var result = await items.SearchAsync(new MenuSearchQuery(branchId, q, maxPrice, tags, AvailableOnly: true, Limit: limit ?? 20), cancellationToken);
        return TypedResults.Ok(result.Select(MenuItemResponse.From));
    }

    public async Task<IResult> GetBatchAsync(Guid branchId, string ids, CancellationToken cancellationToken)
    {
        var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => Guid.TryParse(id, out var value) ? value : (Guid?)null)
            .ToList();

        if (parsed.Count is 0 or > 100 || parsed.Any(id => id is null))
        {
            return TypedResults.Problem(title: "menu.invalid_ids", detail: "Provide 1-100 comma-separated item ids.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await items.GetManyAsync(branchId, parsed.Select(id => id!.Value).ToList(), cancellationToken);
        return TypedResults.Ok(result.Select(i => new { i.Id, i.Name, i.Price, i.Currency, i.IsAvailable }));
    }

    public async Task<IResult> GetAvailabilityAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await items.GetAsync(itemId, cancellationToken);
        return item is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new MenuAvailabilityResponse(item.Id, item.IsAvailable, item.Price, item.Currency, timeProvider.GetUtcNow()));
    }

    public async Task<IResult> CreateAsync(Guid branchId, CreateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var result = workflow.Create(command with { BranchId = branchId });
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/menu/items/{result.Value.Id}", MenuItemResponse.From(result.Value));
    }

    public async Task<IResult> UpdateAsync(Guid itemId, UpdateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var result = await workflow.UpdateAsync(itemId, command, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(MenuItemResponse.From(result.Value));
    }

    public async Task<IResult> DeleteAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var result = await workflow.DeleteAsync(itemId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    public async Task<IResult> SetAvailabilityAsync(Guid itemId, SetAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var result = await workflow.SetAvailabilityAsync(itemId, request.IsAvailable, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(MenuItemResponse.From(result.Value));
    }
}
