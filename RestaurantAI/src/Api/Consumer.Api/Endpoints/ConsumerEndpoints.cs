using Microsoft.EntityFrameworkCore;
using RestaurantAI.Consumer.Api.Persistence;
using RestaurantAI.Consumer.Domain;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Consumer.Api.Endpoints;

public static class ConsumerEndpoints
{
    public sealed record UpdateProfileRequest(string DisplayName, string? PreferredLanguage, IReadOnlyList<string> DietaryPreferences, bool MarketingConsent);

    public sealed record ProfileResponse(Guid Id, string DisplayName, string? PreferredLanguage, IReadOnlyList<string> DietaryPreferences, bool MarketingConsent)
    {
        public static ProfileResponse From(CustomerProfile p) => new(p.Id, p.DisplayName, p.PreferredLanguage, p.DietaryPreferences, p.MarketingConsent);
    }

    public static IEndpointRouteBuilder MapConsumerEndpoints(this IEndpointRouteBuilder app)
    {
        var consumer = app.MapGroup("/api/consumer").WithTags("Consumer");

        consumer.MapGet("/profile", GetProfileAsync).RequirePermission(PlatformPermissions.ConsumerProfile, requireTenant: false);
        consumer.MapPut("/profile", UpdateProfileAsync).RequirePermission(PlatformPermissions.ConsumerProfile, requireTenant: false);

        return app;
    }

    private static async Task<IResult> GetProfileAsync(ITenantContext caller, ConsumerDbContext dbContext, CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == caller.UserId, cancellationToken);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(ProfileResponse.From(profile));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ITenantContext caller,
        ConsumerDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
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
