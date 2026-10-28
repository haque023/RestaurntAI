using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Admin.Api.Endpoints;

/// <summary>
/// Platform administration (verification, plans, entitlements, AI configuration, audit).
/// Every endpoint runs under the separate privileged platform-administrator policy.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(PlatformRoles.PlatformAdministrator));

        admin.MapGet("/overview", (IHostEnvironment environment, TimeProvider timeProvider) => TypedResults.Ok(new
        {
            environment = environment.EnvironmentName,
            serverTime = timeProvider.GetUtcNow(),
        }));

        return app;
    }
}
