using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using RestaurantAI.Identity.Api.Security;
using RestaurantAI.Identity.Domain;
using RestaurantAI.ServiceDefaults.Authentication;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Tests;

public sealed class IdentityTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Passwords_are_salted_and_verified()
    {
        var first = _hasher.Hash("Owner#2026");
        var second = _hasher.Hash("Owner#2026");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("Owner#2026", first));
        Assert.False(_hasher.Verify("owner#2026", first));
        Assert.False(_hasher.Verify("Owner#2026", null));
        Assert.False(_hasher.Verify("Owner#2026", "garbage"));
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void Weak_passwords_are_rejected(string password) => Assert.NotNull(PasswordHasher.Validate(password));

    [Fact]
    public void Repeated_failures_lock_the_account_temporarily()
    {
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.Register("Nadia@Example.com ", "", "hash", now);
        Assert.Equal("nadia@example.com", user.Email);
        Assert.Equal("nadia", user.DisplayName);

        for (var i = 0; i < UserAccount.MaxFailedLogins; i++)
        {
            user.RecordFailedLogin(now);
        }

        Assert.True(user.IsLockedOut(now));
        Assert.False(user.IsLockedOut(now + UserAccount.LockoutDuration + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Memberships_can_be_granted_once_and_revoked_per_tenant()
    {
        var user = UserAccount.Register("karim@example.com", "Karim", "hash", DateTimeOffset.UtcNow);
        var tenant = Guid.NewGuid();
        user.GrantMembership(tenant, "Dhaka Bites", null, PlatformRoles.Waiter);
        user.GrantMembership(tenant, "Dhaka Bites", null, PlatformRoles.Waiter);
        user.GrantMembership(Guid.NewGuid(), "Other", null, PlatformRoles.Cashier);
        Assert.Equal(2, user.Memberships.Count);

        Assert.True(user.RevokeMemberships(tenant));
        Assert.Single(user.Memberships);
    }

    [Fact]
    public void Tokens_carry_role_permissions_never_client_chosen_ones()
    {
        var issuer = new PlatformTokenIssuer(Options.Create(new JwtOptions { SigningKey = "test-signing-key-0123456789abcdef0123456789" }), TimeProvider.System);
        var tenant = Guid.NewGuid();
        var table = Guid.NewGuid();

        var token = issuer.Issue(new TokenGrant(Guid.NewGuid(), tenant, Guid.NewGuid(), table, [PlatformRoles.Customer], TimeSpan.FromHours(1), IsGuest: true));
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.AccessToken);
        var permissions = jwt.Claims.Where(c => c.Type == PlatformClaims.Permission).Select(c => c.Value).ToHashSet();

        Assert.Equal(tenant.ToString(), jwt.GetClaim(PlatformClaims.TenantId).Value);
        Assert.Equal(table.ToString(), jwt.GetClaim(PlatformClaims.TableId).Value);
        Assert.Equal("true", jwt.GetClaim(PlatformTokenIssuer.GuestClaim).Value);
        Assert.Contains(PlatformPermissions.OrdersCreate, permissions);
        Assert.DoesNotContain(PlatformPermissions.OrdersManage, permissions);
        Assert.DoesNotContain(PlatformPermissions.MenuWrite, permissions);
    }

    [Fact]
    public void Role_map_keeps_staff_roles_apart()
    {
        Assert.Contains(PlatformPermissions.StaffManage, PlatformRolePermissions.For(PlatformRoles.RestaurantOwner));
        Assert.DoesNotContain(PlatformPermissions.StaffManage, PlatformRolePermissions.For(PlatformRoles.Waiter));
        Assert.DoesNotContain(PlatformPermissions.RestaurantManage, PlatformRolePermissions.For(PlatformRoles.KitchenStaff));
        Assert.DoesNotContain(PlatformRoles.RestaurantOwner, PlatformRolePermissions.StaffRoles);
        Assert.Empty(PlatformRolePermissions.For("made-up-role"));
    }
}
