using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Services;

/// <summary>Platform-administrator user management (separate privileged policy).</summary>
public sealed class AdminUserService(IdentityDbContext dbContext, PasswordHasher hasher, TimeProvider timeProvider, ITenantContext caller) : IAdminUserService
{
    public async Task<IResult> ListUsersAsync(string? search, int? take, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.Contains(term) || u.DisplayName.ToLower().Contains(term));
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).Take(Math.Clamp(take ?? 100, 1, 500)).ToListAsync(cancellationToken);
        return TypedResults.Ok(users.Select(ToDto).ToList());
    }

    public async Task<IResult> ListTenantMembersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var users = await dbContext.UsersInTenant(tenantId).AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);
        return TypedResults.Ok(users.Select(u => StaffService.ToDto(u, tenantId)).ToList());
    }

    /// <summary>Creates (or links) the owner account for a restaurant the administrator onboarded.</summary>
    public async Task<IResult> CreateOwnerAsync(CreateOwnerRequest request, CancellationToken cancellationToken)
    {
        var email = UserAccount.NormalizeEmail(request.Email ?? string.Empty);
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            if (PasswordHasher.Validate(request.Password) is { } passwordProblem)
            {
                return Problem("auth.weak_password", $"A new owner needs an initial password. {passwordProblem}", StatusCodes.Status400BadRequest);
            }

            user = UserAccount.Register(email, request.DisplayName ?? string.Empty, hasher.Hash(request.Password!), timeProvider.GetUtcNow());
            dbContext.Users.Add(user);
        }

        user.GrantMembership(request.TenantId, request.TenantName, null, PlatformRoles.RestaurantOwner);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(user));
    }

    public Task<IResult> DeactivateAsync(Guid userId, CancellationToken cancellationToken) =>
        SetActiveAsync(userId, false, cancellationToken);

    public Task<IResult> ActivateAsync(Guid userId, CancellationToken cancellationToken) =>
        SetActiveAsync(userId, true, cancellationToken);

    private async Task<IResult> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken)
    {
        if (userId == caller.UserId)
        {
            return Problem("staff.self", "You cannot change your own access.", StatusCodes.Status400BadRequest);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (active)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(user));
    }

    private static AdminUserDto ToDto(UserAccount u) => new(
        u.Id,
        u.Email,
        u.DisplayName,
        u.IsPlatformAdministrator,
        u.IsActive,
        u.CreatedAt,
        u.LastLoginAt,
        u.Memberships.Select(m => new MembershipDto(m.TenantId, m.TenantName, m.BranchId, m.Role)).ToList());

    private static IResult Problem(string title, string detail, int statusCode) =>
        TypedResults.Problem(title: title, detail: detail, statusCode: statusCode);
}
