using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Consumer.Domain;

/// <summary>
/// A consumer's platform-wide profile. Restaurant-specific CRM data lives with the tenant.
/// </summary>
public sealed class CustomerProfile : AggregateRoot<Guid>
{
    private CustomerProfile()
    {
    }

    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? PreferredLanguage { get; private set; }

    public List<string> DietaryPreferences { get; private set; } = [];

    public bool MarketingConsent { get; private set; }

    public DateTimeOffset? MarketingConsentAt { get; private set; }

    public static CustomerProfile Create(Guid userId, string displayName)
    {
        var profile = new CustomerProfile { Id = Guid.CreateVersion7(), UserId = userId, DisplayName = displayName };
        profile.Raise(new CustomerRegistered(profile.Id, userId));
        return profile;
    }

    public void UpdatePreferences(string? preferredLanguage, IEnumerable<string> dietaryPreferences)
    {
        PreferredLanguage = preferredLanguage;
        DietaryPreferences = dietaryPreferences.Select(p => p.Trim().ToLowerInvariant()).Distinct().ToList();
    }

    public void SetMarketingConsent(bool granted, DateTimeOffset at)
    {
        MarketingConsent = granted;
        MarketingConsentAt = at;
    }
}

public sealed record CustomerRegistered(Guid CustomerId, Guid UserId) : DomainEvent;
