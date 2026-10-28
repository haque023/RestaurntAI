using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults.Authorization;

public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Requires an authenticated caller with a tenant context and the given permission.
    /// Platform administrators pass every permission check.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission, bool requireTenant = true)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.AddEndpointFilter(async (context, next) =>
        {
            var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();

            if (requireTenant && tenant.TenantId is null && !tenant.IsPlatformAdministrator)
            {
                return TypedResults.Problem(title: "tenant.required", detail: "A tenant context is required.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (!tenant.HasPermission(permission))
            {
                return TypedResults.Problem(title: "permission.denied", detail: $"Missing permission '{permission}'.", statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });

        return builder;
    }
}
