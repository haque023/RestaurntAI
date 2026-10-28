using RestaurantAI.Menu.Api.Persistence;
using RestaurantAI.Menu.Application.Menu;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Menu.Api.Endpoints;

public static class MenuEndpoints
{
    public sealed record SetAvailabilityRequest(bool IsAvailable);

    public static IEndpointRouteBuilder MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        var menu = app.MapGroup("/api/menu").WithTags("Menu");

        menu.MapGet("/branches/{branchId:guid}/items", GetMenuAsync).RequirePermission(PlatformPermissions.MenuRead);
        menu.MapGet("/branches/{branchId:guid}/items/search", SearchAsync).RequirePermission(PlatformPermissions.MenuRead);
        menu.MapGet("/branches/{branchId:guid}/items/batch", GetBatchAsync).RequirePermission(PlatformPermissions.MenuRead);
        menu.MapGet("/items/{itemId:guid}/availability", GetAvailabilityAsync).RequirePermission(PlatformPermissions.MenuRead);
        menu.MapPost("/branches/{branchId:guid}/items", CreateAsync).RequirePermission(PlatformPermissions.MenuWrite);
        menu.MapPut("/items/{itemId:guid}/availability", SetAvailabilityAsync).RequirePermission(PlatformPermissions.MenuWrite);

        return app;
    }

    private static async Task<IResult> GetMenuAsync(Guid branchId, IMenuItemRepository items, CancellationToken cancellationToken)
    {
        var result = await items.ListForBranchAsync(branchId, cancellationToken);
        return TypedResults.Ok(result.Select(MenuItemResponse.From));
    }

    private static async Task<IResult> SearchAsync(
        Guid branchId,
        string? q,
        decimal? maxPrice,
        string? dietary,
        int? limit,
        IMenuItemRepository items,
        CancellationToken cancellationToken)
    {
        var tags = (dietary ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToList();

        var result = await items.SearchAsync(new MenuSearchQuery(branchId, q, maxPrice, tags, AvailableOnly: true, Limit: limit ?? 20), cancellationToken);
        return TypedResults.Ok(result.Select(MenuItemResponse.From));
    }

    private static async Task<IResult> GetBatchAsync(Guid branchId, string ids, IMenuItemRepository items, CancellationToken cancellationToken)
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

    private static async Task<IResult> GetAvailabilityAsync(Guid itemId, IMenuItemRepository items, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var item = await items.GetAsync(itemId, cancellationToken);
        return item is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new MenuAvailabilityResponse(item.Id, item.IsAvailable, item.Price, item.Currency, timeProvider.GetUtcNow()));
    }

    private static async Task<IResult> CreateAsync(
        Guid branchId,
        CreateMenuItemCommand command,
        MenuService service,
        MenuDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = service.Create(command with { BranchId = branchId });
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/menu/items/{result.Value.Id}", MenuItemResponse.From(result.Value));
    }

    private static async Task<IResult> SetAvailabilityAsync(
        Guid itemId,
        SetAvailabilityRequest request,
        MenuService service,
        MenuDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await service.SetAvailabilityAsync(itemId, request.IsAvailable, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(MenuItemResponse.From(result.Value));
    }
}
