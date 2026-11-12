namespace RestaurantAI.Menu.Api.Models;

public sealed record DishResult(Guid MenuItemId, Guid TenantId, Guid BranchId, string Name, string Category, string Description, IReadOnlyList<string> DietaryTags, decimal Price, string Currency);
