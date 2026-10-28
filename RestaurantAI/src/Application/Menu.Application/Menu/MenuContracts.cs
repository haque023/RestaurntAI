using RestaurantAI.Menu.Domain;

namespace RestaurantAI.Menu.Application.Menu;

public sealed record MenuSearchQuery(Guid BranchId, string? Text, decimal? MaxPrice, IReadOnlyCollection<string> DietaryTags, bool AvailableOnly = true, int Limit = 20);

public sealed record CreateMenuItemCommand(
    Guid BranchId,
    string Category,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    IReadOnlyCollection<string> DietaryTags,
    IReadOnlyCollection<string> Ingredients);

public sealed record UpdateMenuItemCommand(
    string Category,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    IReadOnlyCollection<string> DietaryTags,
    IReadOnlyCollection<string> Ingredients);

public sealed record MenuItemResponse(
    Guid Id,
    Guid BranchId,
    string Category,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    bool IsAvailable,
    IReadOnlyList<string> DietaryTags,
    IReadOnlyList<string> Ingredients)
{
    public static MenuItemResponse From(MenuItem item) => new(
        item.Id,
        item.BranchId,
        item.Category,
        item.Name,
        item.Description,
        item.Price,
        item.Currency,
        item.IsAvailable,
        item.DietaryTags,
        item.Ingredients);
}

public sealed record MenuAvailabilityResponse(Guid MenuItemId, bool IsAvailable, decimal Price, string Currency, DateTimeOffset CheckedAt);

public interface IMenuItemRepository
{
    void Add(MenuItem item);

    void Remove(MenuItem item);

    Task<MenuItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> ListForBranchAsync(Guid branchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> GetManyAsync(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> SearchAsync(MenuSearchQuery query, CancellationToken cancellationToken);
}
