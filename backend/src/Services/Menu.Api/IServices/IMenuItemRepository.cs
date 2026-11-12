namespace RestaurantAI.Menu.Api.IServices;

public interface IMenuItemRepository
{
    void Add(MenuItem item);

    void Remove(MenuItem item);

    Task<MenuItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> ListForBranchAsync(Guid branchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> GetManyAsync(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> SearchAsync(MenuSearchQuery query, CancellationToken cancellationToken);
}
