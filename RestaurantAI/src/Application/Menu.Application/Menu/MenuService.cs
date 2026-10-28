using RestaurantAI.Menu.Domain;
using RestaurantAI.SharedKernel.Domain;
using RestaurantAI.SharedKernel.Results;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Menu.Application.Menu;

public sealed class MenuService(IMenuItemRepository items, ITenantContext tenant)
{
    public Result<MenuItem> Create(CreateMenuItemCommand command)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return Error.Forbidden("tenant.required", "A tenant context is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Name) || command.Currency.Length != 3)
        {
            return Error.Validation("menu.invalid_item", "Name and a 3-letter ISO currency are required.");
        }

        var item = MenuItem.Create(
            tenantId,
            command.BranchId,
            command.Category,
            command.Name.Trim(),
            command.Description,
            new Money(command.Price, command.Currency.ToUpperInvariant()),
            command.DietaryTags,
            command.Ingredients);

        items.Add(item);
        return item;
    }

    public async Task<Result<MenuItem>> UpdateAsync(Guid id, UpdateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var item = await items.GetAsync(id, cancellationToken);
        if (item is null || !CanAccessBranch(item.BranchId))
        {
            return Error.NotFound("menu.item_not_found", "Menu item not found.");
        }

        if (string.IsNullOrWhiteSpace(command.Name) || command.Currency?.Length != 3)
        {
            return Error.Validation("menu.invalid_item", "Name and a 3-letter ISO currency are required.");
        }

        item.UpdateDetails(
            command.Category ?? string.Empty,
            command.Name,
            command.Description ?? string.Empty,
            new Money(command.Price, command.Currency.ToUpperInvariant()),
            command.DietaryTags ?? [],
            command.Ingredients ?? []);
        return item;
    }

    public async Task<Result<MenuItem>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await items.GetAsync(id, cancellationToken);
        if (item is null || !CanAccessBranch(item.BranchId))
        {
            return Error.NotFound("menu.item_not_found", "Menu item not found.");
        }

        items.Remove(item);
        return item;
    }

    private bool CanAccessBranch(Guid branchId) =>
        tenant.IsPlatformAdministrator || tenant.BranchId is null || tenant.BranchId == branchId;

    public async Task<Result<MenuItem>> SetAvailabilityAsync(Guid id, bool isAvailable, CancellationToken cancellationToken)
    {
        var item = await items.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return Error.NotFound("menu.item_not_found", "Menu item not found.");
        }

        item.SetAvailability(isAvailable);
        return item;
    }
}
