using RestaurantAI.Consumer.Domain;

namespace RestaurantAI.Consumer.Tests;

public sealed class CustomerProfileTests
{
    [Fact]
    public void Create_raises_CustomerRegistered()
    {
        var profile = CustomerProfile.Create(Guid.NewGuid(), "Sam");

        Assert.IsType<CustomerRegistered>(Assert.Single(profile.DomainEvents));
    }

    [Fact]
    public void UpdatePreferences_normalizes_and_deduplicates_dietary_preferences()
    {
        var profile = CustomerProfile.Create(Guid.NewGuid(), "Sam");

        profile.UpdatePreferences("en", [" Vegan", "vegan", "Gluten-Free "]);

        Assert.Equal(["vegan", "gluten-free"], profile.DietaryPreferences);
    }

    [Fact]
    public void SetMarketingConsent_records_when_consent_changed()
    {
        var profile = CustomerProfile.Create(Guid.NewGuid(), "Sam");
        var at = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        profile.SetMarketingConsent(true, at);

        Assert.True(profile.MarketingConsent);
        Assert.Equal(at, profile.MarketingConsentAt);
    }
}
