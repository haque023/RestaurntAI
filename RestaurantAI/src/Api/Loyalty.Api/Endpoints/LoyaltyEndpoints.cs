using RestaurantAI.Loyalty.Application.Loyalty;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Loyalty.Api.Endpoints;

public static class LoyaltyEndpoints
{
    public static IEndpointRouteBuilder MapLoyaltyEndpoints(this IEndpointRouteBuilder app)
    {
        var loyalty = app.MapGroup("/api/loyalty").WithTags("Loyalty");

        loyalty.MapGet("/balance", GetBalanceAsync).RequirePermission(PlatformPermissions.LoyaltyRead);

        return app;
    }

    private static async Task<IResult> GetBalanceAsync(ITenantContext caller, LoyaltyService service, CancellationToken cancellationToken)
    {
        if (caller.TenantId is not { } tenantId || caller.UserId is not { } userId)
        {
            return TypedResults.Forbid();
        }

        var result = await service.GetBalanceAsync(tenantId, userId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
