using RestaurantAI.ServiceDefaults.Authentication;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Gateway.Api.Configuration;

/// <summary>
/// The gateway validates tokens at the edge of the platform and forwards them unchanged;
/// downstream services validate them again (defense in depth).
/// </summary>
public static class AuthenticationConfig
{
    /// <summary>Route policy: any authenticated caller.</summary>
    public const string Authenticated = "authenticated";

    /// <summary>Route policy: platform administrators only.</summary>
    public const string PlatformAdministrator = "platform-admin";

    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddPlatformJwtBearer(configuration, environment);

        services.AddAuthorizationBuilder()
            .AddPolicy(Authenticated, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(PlatformAdministrator, policy => policy.RequireAuthenticatedUser().RequireRole(PlatformRoles.PlatformAdministrator));

        return services;
    }
}
