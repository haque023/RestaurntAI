using RestaurantAI.Order.Application.Abstractions;

namespace RestaurantAI.Order.Api.Integration;

/// <summary>Reads authoritative prices and availability from the Menu service.</summary>
internal sealed class MenuCatalogClient(HttpClient httpClient) : IMenuCatalog
{
    public async Task<IReadOnlyDictionary<Guid, CatalogItem>> GetItemsAsync(
        Guid branchId,
        IReadOnlyCollection<Guid> menuItemIds,
        CancellationToken cancellationToken)
    {
        if (menuItemIds.Count == 0)
        {
            return new Dictionary<Guid, CatalogItem>();
        }

        var ids = string.Join(',', menuItemIds);
        var items = await httpClient.GetFromJsonAsync<List<CatalogItem>>(
            $"/api/menu/branches/{branchId}/items/batch?ids={ids}",
            cancellationToken) ?? [];

        return items.ToDictionary(i => i.Id);
    }
}
