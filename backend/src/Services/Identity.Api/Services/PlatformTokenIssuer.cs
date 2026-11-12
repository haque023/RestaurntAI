using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RestaurantAI.ServiceDefaults.Authentication;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Services;

/// <summary>What one access token grants: the caller, their tenant/branch/table scope and roles.</summary>
public sealed record TokenGrant(
    Guid UserId,
    Guid? TenantId,
    Guid? BranchId,
    Guid? TableId,
    IReadOnlyList<string> Roles,
    TimeSpan Lifetime,
    string? Email = null,
    string? DisplayName = null,
    bool IsGuest = false,
    IReadOnlyList<string>? ExtraPermissions = null);

/// <summary>
/// Issues the platform's access tokens. Permissions always come from the role map, never from
/// the client. Signed with the configured symmetric key; an OIDC provider can take over later.
/// </summary>
public sealed class PlatformTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public const string GuestClaim = "guest";
    public const string EmailClaim = "email";
    public const string NameClaim = "name";

    public IssuedToken Issue(TokenGrant grant)
    {
        var jwt = options.Value;
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must be configured to issue tokens.");
        }

        var claims = new List<Claim> { new(PlatformClaims.Subject, grant.UserId.ToString()) };
        AddIfPresent(claims, PlatformClaims.TenantId, grant.TenantId);
        AddIfPresent(claims, PlatformClaims.BranchId, grant.BranchId);
        AddIfPresent(claims, PlatformClaims.TableId, grant.TableId);

        if (grant.Email is not null)
        {
            claims.Add(new Claim(EmailClaim, grant.Email));
        }

        if (grant.DisplayName is not null)
        {
            claims.Add(new Claim(NameClaim, grant.DisplayName));
        }

        if (grant.IsGuest)
        {
            claims.Add(new Claim(GuestClaim, "true"));
        }

        claims.AddRange(grant.Roles.Distinct().Select(r => new Claim(PlatformClaims.Role, r)));
        var permissions = grant.Roles.SelectMany(PlatformRolePermissions.For).Concat(grant.ExtraPermissions ?? []).Distinct();
        claims.AddRange(permissions.Select(p => new Claim(PlatformClaims.Permission, p)));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.Add(grant.Lifetime);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), (int)grant.Lifetime.TotalSeconds);
    }

    private static void AddIfPresent(List<Claim> claims, string type, Guid? value)
    {
        if (value is { } id)
        {
            claims.Add(new Claim(type, id.ToString()));
        }
    }
}

public sealed record IssuedToken(string AccessToken, int ExpiresIn);
