namespace RestaurantAI.Order.Application.Abstractions;

public sealed record CatalogItem(Guid Id, string Name, decimal Price, string Currency, bool IsAvailable);

/// <summary>
/// Authoritative price and availability lookup, served by the Menu service.
/// Orders never trust prices supplied by clients or AI agents.
/// </summary>
public interface IMenuCatalog
{
    Task<IReadOnlyDictionary<Guid, CatalogItem>> GetItemsAsync(Guid branchId, IReadOnlyCollection<Guid> menuItemIds, CancellationToken cancellationToken);
}
