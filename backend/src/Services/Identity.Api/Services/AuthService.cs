using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Services;

public sealed class AuthService(PlatformTokenIssuer issuer, IHostEnvironment environment, IdentityDbContext dbContext, PasswordHasher hasher, TimeProvider timeProvider, ITenantContext caller, PlatformServicesClient platform) : IAuthService
{
    public static readonly TimeSpan CustomerLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan StaffLifetime = TimeSpan.FromHours(10);
    public static readonly TimeSpan GuestLifetime = TimeSpan.FromHours(6);

    public IResult Me(ClaimsPrincipal user) => TypedResults.Ok(new
    {
        sub = user.FindFirstValue(PlatformClaims.Subject),
        tenantId = user.FindFirstValue(PlatformClaims.TenantId),
        branchId = user.FindFirstValue(PlatformClaims.BranchId),
        roles = user.FindAll(PlatformClaims.Role).Select(c => c.Value),
        permissions = user.FindAll(PlatformClaims.Permission).Select(c => c.Value),
    });

    /// <summary>Local development only: lets test scripts mint arbitrary tokens. Does not exist elsewhere.</summary>
    public IResult IssueDevToken(DevTokenRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return TypedResults.NotFound();
        }

        var token = issuer.Issue(new TokenGrant(
            request.UserId ?? Guid.NewGuid(),
            request.TenantId,
            request.BranchId,
            null,
            request.Roles,
            TimeSpan.FromMinutes(Math.Clamp(request.LifetimeMinutes ?? 60, 1, 24 * 60)),
            ExtraPermissions: request.Permissions));
        return TypedResults.Ok(new { access_token = token.AccessToken, token_type = "Bearer" });
    }

    public async Task<IResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (PasswordHasher.Validate(request.Password) is { } passwordProblem)
        {
            return Problem("auth.weak_password", passwordProblem, StatusCodes.Status400BadRequest);
        }

        var email = UserAccount.NormalizeEmail(request.Email ?? string.Empty);
        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Problem("auth.email_taken", "An account with this email already exists. Sign in instead.", StatusCodes.Status409Conflict);
        }

        var user = UserAccount.Register(email, request.DisplayName ?? string.Empty, hasher.Hash(request.Password), timeProvider.GetUtcNow());
        user.RecordSuccessfulLogin(timeProvider.GetUtcNow());
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(IssueSession(user, SessionScope.Customer, null).Session!);
    }

    public async Task<IResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = UserAccount.NormalizeEmail(request.Email ?? string.Empty);
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Same work as a real check, so response time does not reveal which emails exist.
            hasher.Verify(request.Password ?? string.Empty, DummyHash.Value);
            return InvalidCredentials();
        }

        if (user.IsLockedOut(now))
        {
            return Problem("auth.locked_out", "Too many failed sign-in attempts. Try again in a few minutes.", StatusCodes.Status429TooManyRequests);
        }

        if (!hasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            user.RecordFailedLogin(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return InvalidCredentials();
        }

        if (!user.IsActive)
        {
            return Problem("auth.disabled", "This account has been disabled. Contact support.", StatusCodes.Status403Forbidden);
        }

        user.RecordSuccessfulLogin(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        var issued = IssueSession(user, request.Scope ?? SessionScope.Customer, request.TenantId);
        return issued.Session is { } session ? TypedResults.Ok(session) : issued.Problem!;
    }

    public async Task<IResult> SwitchTenantAsync(SwitchTenantRequest request, CancellationToken cancellationToken)
    {
        var user = caller.UserId is { } id ? await dbContext.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken) : null;
        if (user is null || !user.IsActive)
        {
            return Problem("auth.unknown_user", "Sign in again.", StatusCodes.Status401Unauthorized);
        }

        var issued = IssueSession(user, SessionScope.Staff, request.TenantId);
        return issued.Session is { } session ? TypedResults.Ok(session) : issued.Problem!;
    }

    public async Task<IResult> GetMembershipsAsync(CancellationToken cancellationToken)
    {
        var user = caller.UserId is { } id ? await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken) : null;
        return TypedResults.Ok(user?.Memberships.Select(ToDto).ToList() ?? []);
    }

    /// <summary>
    /// Scopes a customer (or an anonymous guest who scanned a table QR) to one restaurant. Guests
    /// get a temporary identity so they can talk to the AI waiter and order without an account.
    /// </summary>
    public async Task<IResult> VenueSessionAsync(VenueSessionRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        VenueDto venue;
        if (!string.IsNullOrWhiteSpace(request.QrToken))
        {
            var qr = await platform.ResolveQrAsync(request.QrToken.Trim(), cancellationToken);
            if (qr is null)
            {
                return Problem("venue.qr_unknown", "This table code is not valid or the restaurant is not open on the platform.", StatusCodes.Status404NotFound);
            }

            venue = new VenueDto(qr.TenantId, qr.BranchId, qr.TableId, qr.RestaurantName, qr.BranchName, qr.TableLabel);
        }
        else if (request.TenantId is { } tenantId)
        {
            var listed = await platform.GetListedRestaurantAsync(tenantId, cancellationToken);
            var branch = request.BranchId is { } branchId ? listed?.Branches.FirstOrDefault(b => b.BranchId == branchId) : null;
            if (listed is null || (request.BranchId is not null && branch is null))
            {
                return Problem("venue.not_listed", "This restaurant is not available.", StatusCodes.Status404NotFound);
            }

            venue = new VenueDto(listed.TenantId, branch?.BranchId, null, listed.RestaurantName, branch?.BranchName, null);
        }
        else
        {
            return Problem("venue.required", "Provide a table code or a restaurant.", StatusCodes.Status400BadRequest);
        }

        var signedIn = principal.Identity?.IsAuthenticated == true && Guid.TryParse(principal.FindFirstValue(PlatformClaims.Subject), out _);
        var isGuest = !signedIn || principal.HasClaim(PlatformTokenIssuer.GuestClaim, "true");
        var userId = signedIn ? Guid.Parse(principal.FindFirstValue(PlatformClaims.Subject)!) : Guid.CreateVersion7();
        var account = isGuest ? null : await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        var token = issuer.Issue(new TokenGrant(
            userId,
            venue.TenantId,
            venue.BranchId,
            venue.TableId,
            [PlatformRoles.Customer],
            isGuest ? GuestLifetime : CustomerLifetime,
            account?.Email,
            account?.DisplayName,
            IsGuest: isGuest));

        return TypedResults.Ok(new SessionResponse(
            token.AccessToken,
            token.ExpiresIn,
            new SessionUserDto(userId, account?.Email, account?.DisplayName, false, isGuest),
            [],
            venue));
    }

    /// <summary>Self-service onboarding: creates the owner account and a restaurant awaiting verification.</summary>
    public async Task<IResult> SignupRestaurantAsync(RestaurantSignupRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RestaurantName) || request.RestaurantName.Length > 200)
        {
            return Problem("restaurant.invalid", "Restaurant name is required (up to 200 characters).", StatusCodes.Status400BadRequest);
        }

        var email = UserAccount.NormalizeEmail(request.Email ?? string.Empty);
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            if (PasswordHasher.Validate(request.Password) is { } passwordProblem)
            {
                return Problem("auth.weak_password", passwordProblem, StatusCodes.Status400BadRequest);
            }
        }
        else if (!hasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            return Problem("auth.email_taken", "An account with this email already exists. Use its password to add a restaurant to it.", StatusCodes.Status409Conflict);
        }

        CreatedRestaurant restaurant;
        try
        {
            restaurant = await platform.CreateRestaurantAsync(
                request.RestaurantName.Trim(),
                (request.Currency ?? "USD").Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone.Trim(),
                selfService: true,
                cancellationToken);
        }
        catch (PlatformServiceException ex) when (ex.StatusCode is >= 400 and < 500)
        {
            return Problem("restaurant.invalid", "The restaurant details were rejected. Check the currency (3 letters) and time zone.", StatusCodes.Status400BadRequest);
        }

        var now = timeProvider.GetUtcNow();
        if (user is null)
        {
            user = UserAccount.Register(email, request.DisplayName ?? string.Empty, hasher.Hash(request.Password!), now);
            dbContext.Users.Add(user);
        }

        user.GrantMembership(restaurant.Id, restaurant.Name, null, PlatformRoles.RestaurantOwner);
        user.RecordSuccessfulLogin(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(IssueSession(user, SessionScope.Staff, restaurant.Id).Session!);
    }

    private (SessionResponse? Session, IResult? Problem) IssueSession(UserAccount user, SessionScope scope, Guid? tenantId)
    {
        var memberships = user.Memberships.Select(ToDto).ToList();
        var sessionUser = new SessionUserDto(user.Id, user.Email, user.DisplayName, user.IsPlatformAdministrator, false);

        TokenGrant grant;
        switch (scope)
        {
            case SessionScope.Admin:
                if (!user.IsPlatformAdministrator)
                {
                    return (null, Problem("auth.not_admin", "This account is not a platform administrator.", StatusCodes.Status403Forbidden));
                }

                grant = new TokenGrant(user.Id, null, null, null, [PlatformRoles.PlatformAdministrator], StaffLifetime, user.Email, user.DisplayName);
                break;

            case SessionScope.Staff:
                var tenant = tenantId ?? user.Memberships.FirstOrDefault()?.TenantId;
                var inTenant = user.Memberships.Where(m => m.TenantId == tenant).ToList();
                if (tenant is null || inTenant.Count == 0)
                {
                    return (null, Problem(
                        "auth.no_membership",
                        "This account does not belong to a restaurant yet. Register your restaurant or ask its owner to add you.",
                        StatusCodes.Status403Forbidden));
                }

                // Unrestricted if any membership covers the whole restaurant.
                var branch = inTenant.Any(m => m.BranchId is null) ? null : inTenant[0].BranchId;
                grant = new TokenGrant(user.Id, tenant, branch, null, inTenant.Select(m => m.Role).ToList(), StaffLifetime, user.Email, user.DisplayName);
                break;

            default:
                grant = new TokenGrant(user.Id, null, null, null, [PlatformRoles.Customer], CustomerLifetime, user.Email, user.DisplayName);
                break;
        }

        var token = issuer.Issue(grant);
        return (new SessionResponse(token.AccessToken, token.ExpiresIn, sessionUser, memberships), null);
    }

    private static MembershipDto ToDto(TenantMembership m) => new(m.TenantId, m.TenantName, m.BranchId, m.Role);

    private static IResult InvalidCredentials() =>
        Problem("auth.invalid_credentials", "The email or password is incorrect.", StatusCodes.Status401Unauthorized);

    private static IResult Problem(string title, string detail, int statusCode) =>
        TypedResults.Problem(title: title, detail: detail, statusCode: statusCode);

    private static class DummyHash
    {
        public static readonly string Value = new PasswordHasher().Hash("not-a-real-password-1");
    }
}
