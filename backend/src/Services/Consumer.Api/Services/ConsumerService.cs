using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Consumer.Api.Services;

public sealed class ConsumerService(ITenantContext caller, ConsumerDbContext dbContext, TimeProvider timeProvider) : IConsumerService
{
    public async Task<IResult> GetProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == caller.UserId, cancellationToken);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(ProfileResponse.From(profile));
    }

    public async Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (caller.UserId is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var profile = await dbContext.Profiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = CustomerProfile.Create(userId, request.DisplayName);
            dbContext.Profiles.Add(profile);
        }

        profile.UpdatePreferences(request.PreferredLanguage, request.DietaryPreferences);
        if (profile.MarketingConsent != request.MarketingConsent)
        {
            profile.SetMarketingConsent(request.MarketingConsent, timeProvider.GetUtcNow());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ProfileResponse.From(profile));
    }
}
