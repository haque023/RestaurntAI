using Microsoft.EntityFrameworkCore;

namespace RestaurantAI.Menu.Api.Data;

internal sealed class MenuItemRepository(MenuDbContext dbContext) : IMenuItemRepository
{
    public void Add(MenuItem item) => dbContext.MenuItems.Add(item);

    public void Remove(MenuItem item) => dbContext.MenuItems.Remove(item);

    public Task<MenuItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.MenuItems.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MenuItem>> ListForBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
        await dbContext.MenuItems.AsNoTracking()
            .Where(i => i.BranchId == branchId)
            .OrderBy(i => i.Category).ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MenuItem>> GetManyAsync(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.MenuItems.AsNoTracking()
            .Where(i => i.BranchId == branchId && ids.Contains(i.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MenuItem>> SearchAsync(MenuSearchQuery query, CancellationToken cancellationToken)
    {
        var items = dbContext.MenuItems.AsNoTracking().Where(i => i.BranchId == query.BranchId);

        if (query.AvailableOnly)
        {
            items = items.Where(i => i.IsAvailable);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var pattern = $"%{query.Text.Trim()}%";
            items = items.Where(i => EF.Functions.ILike(i.Name, pattern) || EF.Functions.ILike(i.Description, pattern) || EF.Functions.ILike(i.Category, pattern));
        }

        if (query.MaxPrice is { } maxPrice)
        {
            items = items.Where(i => i.Price <= maxPrice);
        }

        foreach (var tag in query.DietaryTags)
        {
            items = items.Where(i => i.DietaryTags.Contains(tag));
        }

        return await items.OrderBy(i => i.Price).Take(Math.Clamp(query.Limit, 1, 100)).ToListAsync(cancellationToken);
    }
}
