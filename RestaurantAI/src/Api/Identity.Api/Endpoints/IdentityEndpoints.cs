using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RestaurantAI.ServiceDefaults.Authentication;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Endpoints;

public static class IdentityEndpoints
{
    public sealed record DevTokenRequest(
        Guid? UserId,
        Guid? TenantId,
        Guid? BranchId,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        int? LifetimeMinutes);

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Identity");

        auth.MapGet("/me", (ClaimsPrincipal user) => TypedResults.Ok(new
        {
            sub = user.FindFirstValue(PlatformClaims.Subject),
            tenantId = user.FindFirstValue(PlatformClaims.TenantId),
            branchId = user.FindFirstValue(PlatformClaims.BranchId),
            roles = user.FindAll(PlatformClaims.Role).Select(c => c.Value),
            permissions = user.FindAll(PlatformClaims.Permission).Select(c => c.Value),
        })).RequireAuthorization();

        // Local development only: production tokens come from the central identity provider.
        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            auth.MapPost("/dev-token", (DevTokenRequest request, DevelopmentTokenIssuer issuer) =>
                TypedResults.Ok(new { access_token = issuer.Issue(request), token_type = "Bearer" }))
                .AllowAnonymous();
        }

        return app;
    }
}

public sealed class DevelopmentTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public string Issue(IdentityEndpoints.DevTokenRequest request)
    {
        var jwt = options.Value;
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must be configured to issue development tokens.");
        }

        var claims = new List<Claim> { new(PlatformClaims.Subject, (request.UserId ?? Guid.NewGuid()).ToString()) };
        if (request.TenantId is { } tenantId)
        {
            claims.Add(new Claim(PlatformClaims.TenantId, tenantId.ToString()));
        }

        if (request.BranchId is { } branchId)
        {
            claims.Add(new Claim(PlatformClaims.BranchId, branchId.ToString()));
        }

        claims.AddRange(request.Roles.Select(r => new Claim(PlatformClaims.Role, r)));
        claims.AddRange(request.Permissions.Select(p => new Claim(PlatformClaims.Permission, p)));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = now.AddMinutes(Math.Clamp(request.LifetimeMinutes ?? 60, 1, 24 * 60)),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
