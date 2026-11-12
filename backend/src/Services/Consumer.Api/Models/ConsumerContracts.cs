namespace RestaurantAI.Consumer.Api.Models;

public sealed record UpdateProfileRequest(string DisplayName, string? PreferredLanguage, IReadOnlyList<string> DietaryPreferences, bool MarketingConsent);

public sealed record ProfileResponse(Guid Id, string DisplayName, string? PreferredLanguage, IReadOnlyList<string> DietaryPreferences, bool MarketingConsent)
{
    public static ProfileResponse From(CustomerProfile p) => new(p.Id, p.DisplayName, p.PreferredLanguage, p.DietaryPreferences, p.MarketingConsent);
}
