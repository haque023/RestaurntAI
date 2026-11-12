using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Menu.Api.Models;

/// <summary>
/// A sellable menu item. The menu service is the authority for price and availability;
/// AI and RAG content never override these values.
/// </summary>
public sealed class MenuItem : AggregateRoot<Guid>, IBranchScoped
{
    private MenuItem()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = "USD";

    public bool IsAvailable { get; private set; }

    public List<string> DietaryTags { get; private set; } = [];

    public List<string> Ingredients { get; private set; } = [];

    public static MenuItem Create(
        Guid tenantId,
        Guid branchId,
        string category,
        string name,
        string description,
        Money price,
        IEnumerable<string> dietaryTags,
        IEnumerable<string> ingredients)
    {
        if (price.Amount < 0)
        {
            throw new DomainException("menu.invalid_price", "Price cannot be negative.");
        }

        var item = new MenuItem
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BranchId = branchId,
            Category = category,
            Name = name,
            Description = description,
            Price = price.Amount,
            Currency = price.Currency,
            IsAvailable = true,
            DietaryTags = dietaryTags.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList(),
            Ingredients = ingredients.ToList(),
        };
        item.Raise(new MenuItemChanged(item.Id, tenantId, branchId));
        return item;
    }

    public Money UnitPrice => new(Price, Currency);

    public void ChangePrice(Money price)
    {
        if (price.Amount < 0)
        {
            throw new DomainException("menu.invalid_price", "Price cannot be negative.");
        }

        Price = price.Amount;
        Currency = price.Currency;
        Raise(new MenuItemChanged(Id, TenantId, BranchId));
    }

    public void UpdateDetails(
        string category,
        string name,
        string description,
        Money price,
        IEnumerable<string> dietaryTags,
        IEnumerable<string> ingredients)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("menu.name_required", "Item name is required.");
        }

        if (price.Amount < 0)
        {
            throw new DomainException("menu.invalid_price", "Price cannot be negative.");
        }

        Category = category.Trim();
        Name = name.Trim();
        Description = description.Trim();
        Price = price.Amount;
        Currency = price.Currency;
        DietaryTags = dietaryTags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList();
        Ingredients = ingredients.Select(i => i.Trim()).Where(i => i.Length > 0).ToList();
        Raise(new MenuItemChanged(Id, TenantId, BranchId));
    }

    public void SetAvailability(bool isAvailable)
    {
        if (IsAvailable == isAvailable)
        {
            return;
        }

        IsAvailable = isAvailable;
        Raise(new MenuItemChanged(Id, TenantId, BranchId));
    }
}

/// <summary>Raised on any change so caches and the RAG index can refresh.</summary>
public sealed record MenuItemChanged(Guid MenuItemId, Guid TenantId, Guid BranchId) : DomainEvent;
