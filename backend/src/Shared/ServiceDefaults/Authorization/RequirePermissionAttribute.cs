using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults.Authorization;

/// <summary>
/// Requires an authenticated caller with a tenant context and the given permission.
/// Platform administrators pass every permission check.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute, IAuthorizationFilter
{
    public string Permission { get; } = permission;

    public bool RequireTenant { get; set; } = true;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();

        if (RequireTenant && tenant.TenantId is null && !tenant.IsPlatformAdministrator)
        {
            context.Result = Denied("tenant.required", "A tenant context is required.");
        }
        else if (!tenant.HasPermission(Permission))
        {
            context.Result = Denied("permission.denied", $"Missing permission '{Permission}'.");
        }
    }

    private static ProblemResult Denied(string title, string detail) =>
        new(TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status403Forbidden));

    private sealed class ProblemResult(IResult problem) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context) => problem.ExecuteAsync(context.HttpContext);
    }
}
