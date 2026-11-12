using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Identity.Api.Services;

/// <summary>
/// Restaurant staff accounts: owners and managers add people with a role and an optional branch.
/// Permissions always follow the role (BR-007); only owners can appoint managers.
/// </summary>
public sealed class StaffService(ITenantContext caller, IdentityDbContext dbContext, PasswordHasher hasher, TimeProvider timeProvider) : IStaffService
{
    public IResult GetRoles() =>
        TypedResults.Ok(PlatformRolePermissions.StaffRoles.Select(r => new { role = r, permissions = PlatformRolePermissions.For(r) }));

    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        var members = await dbContext.UsersInTenant(tenantId).AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);
        return TypedResults.Ok(members.Select(u => ToDto(u, tenantId)).ToList());
    }

    public async Task<IResult> AddAsync(AddStaffRequest request, CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        if (CheckAssignment(request.Role, request.BranchId) is { } denied)
        {
            return denied;
        }

        var email = UserAccount.NormalizeEmail(request.Email ?? string.Empty);
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            if (PasswordHasher.Validate(request.Password) is { } passwordProblem)
            {
                return Problem("auth.weak_password", $"New staff need an initial password. {passwordProblem}", StatusCodes.Status400BadRequest);
            }

            user = UserAccount.Register(email, request.DisplayName ?? string.Empty, hasher.Hash(request.Password!), timeProvider.GetUtcNow());
            dbContext.Users.Add(user);
        }
        else if (user.Memberships.Any(m => m.TenantId == tenantId))
        {
            return Problem("staff.exists", "This person is already on your team. Change their role instead.", StatusCodes.Status409Conflict);
        }

        user.GrantMembership(tenantId, await TenantNameAsync(cancellationToken), request.BranchId, request.Role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/auth/staff/{user.Id}", ToDto(user, tenantId));
    }

    public async Task<IResult> UpdateAsync(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        if (CheckAssignment(request.Role, request.BranchId) is { } denied)
        {
            return denied;
        }

        var user = await dbContext.UsersInTenant(tenantId).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (CheckTarget(user, tenantId) is { } protectedMember)
        {
            return protectedMember;
        }

        var tenantName = user.Memberships.First(m => m.TenantId == tenantId).TenantName;
        user.RevokeMemberships(tenantId);
        user.GrantMembership(tenantId, tenantName, request.BranchId, request.Role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(user, tenantId));
    }

    public async Task<IResult> RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        var user = await dbContext.UsersInTenant(tenantId).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (CheckTarget(user, tenantId) is { } protectedMember)
        {
            return protectedMember;
        }

        user.RevokeMemberships(tenantId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private IResult? CheckAssignment(string role, Guid? branchId)
    {
        if (!PlatformRolePermissions.StaffRoles.Contains(role))
        {
            return Problem("staff.invalid_role", $"Role must be one of: {string.Join(", ", PlatformRolePermissions.StaffRoles)}.", StatusCodes.Status400BadRequest);
        }

        if (role == PlatformRoles.RestaurantManager && !caller.Roles.Contains(PlatformRoles.RestaurantOwner) && !caller.IsPlatformAdministrator)
        {
            return Problem("staff.owner_only", "Only the restaurant owner can appoint managers.", StatusCodes.Status403Forbidden);
        }

        // A branch-restricted manager can only staff their own branch.
        if (caller.BranchId is { } own && branchId != own)
        {
            return Problem("branch.forbidden", "You can only add staff to your own branch.", StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private IResult? CheckTarget(UserAccount user, Guid tenantId)
    {
        if (user.Id == caller.UserId)
        {
            return Problem("staff.self", "You cannot change your own access.", StatusCodes.Status400BadRequest);
        }

        var roles = user.Memberships.Where(m => m.TenantId == tenantId).Select(m => m.Role).ToList();
        if (roles.Contains(PlatformRoles.RestaurantOwner) && !caller.IsPlatformAdministrator)
        {
            return Problem("staff.owner_protected", "The owner's access can only be changed by platform support.", StatusCodes.Status403Forbidden);
        }

        if (roles.Contains(PlatformRoles.RestaurantManager) && !caller.Roles.Contains(PlatformRoles.RestaurantOwner) && !caller.IsPlatformAdministrator)
        {
            return Problem("staff.owner_only", "Only the restaurant owner can change a manager.", StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private async Task<string> TenantNameAsync(CancellationToken cancellationToken)
    {
        var self = caller.UserId is { } id ? await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken) : null;
        return self?.Memberships.FirstOrDefault(m => m.TenantId == caller.TenantId)?.TenantName ?? string.Empty;
    }

    internal static StaffMemberDto ToDto(UserAccount user, Guid tenantId) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.IsActive,
        user.LastLoginAt,
        user.Memberships.Where(m => m.TenantId == tenantId).Select(m => new MembershipDto(m.TenantId, m.TenantName, m.BranchId, m.Role)).ToList());

    private static IResult Problem(string title, string detail, int statusCode) =>
        TypedResults.Problem(title: title, detail: detail, statusCode: statusCode);
}
