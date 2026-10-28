using Microsoft.EntityFrameworkCore;
using RestaurantAI.Identity.Api.Persistence;
using RestaurantAI.Identity.Api.Security;
using RestaurantAI.Identity.Domain;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;
using static RestaurantAI.Identity.Api.Endpoints.IdentityEndpoints;

namespace RestaurantAI.Identity.Api.Endpoints;

/// <summary>
/// Restaurant staff accounts: owners and managers add people with a role and an optional branch.
/// Permissions always follow the role (BR-007); only owners can appoint managers.
/// </summary>
public static class StaffEndpoints
{
    public sealed record StaffMemberDto(Guid UserId, string Email, string DisplayName, bool IsActive, DateTimeOffset? LastLoginAt, IReadOnlyList<MembershipDto> Roles);

    public sealed record AddStaffRequest(string Email, string? DisplayName, string? Password, string Role, Guid? BranchId);

    public sealed record UpdateStaffRequest(string Role, Guid? BranchId);

    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api/auth/staff").WithTags("Staff");

        staff.MapGet("/", ListAsync).RequirePermission(PlatformPermissions.StaffManage);
        staff.MapGet("/roles", () => TypedResults.Ok(PlatformRolePermissions.StaffRoles.Select(r => new { role = r, permissions = PlatformRolePermissions.For(r) })))
            .RequirePermission(PlatformPermissions.StaffManage);
        staff.MapPost("/", AddAsync).RequirePermission(PlatformPermissions.StaffManage);
        staff.MapPut("/{userId:guid}", UpdateAsync).RequirePermission(PlatformPermissions.StaffManage);
        staff.MapDelete("/{userId:guid}", RemoveAsync).RequirePermission(PlatformPermissions.StaffManage);

        return app;
    }

    private static async Task<IResult> ListAsync(ITenantContext caller, IdentityDbContext dbContext, CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        var members = await dbContext.UsersInTenant(tenantId).AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);
        return TypedResults.Ok(members.Select(u => ToDto(u, tenantId)).ToList());
    }

    private static async Task<IResult> AddAsync(
        AddStaffRequest request,
        ITenantContext caller,
        IdentityDbContext dbContext,
        PasswordHasher hasher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        if (CheckAssignment(caller, request.Role, request.BranchId) is { } denied)
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

        user.GrantMembership(tenantId, await TenantNameAsync(dbContext, caller, cancellationToken), request.BranchId, request.Role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/auth/staff/{user.Id}", ToDto(user, tenantId));
    }

    private static async Task<IResult> UpdateAsync(
        Guid userId,
        UpdateStaffRequest request,
        ITenantContext caller,
        IdentityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        if (CheckAssignment(caller, request.Role, request.BranchId) is { } denied)
        {
            return denied;
        }

        var user = await dbContext.UsersInTenant(tenantId).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (CheckTarget(caller, user, tenantId) is { } protectedMember)
        {
            return protectedMember;
        }

        var tenantName = user.Memberships.First(m => m.TenantId == tenantId).TenantName;
        user.RevokeMemberships(tenantId);
        user.GrantMembership(tenantId, tenantName, request.BranchId, request.Role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(user, tenantId));
    }

    private static async Task<IResult> RemoveAsync(Guid userId, ITenantContext caller, IdentityDbContext dbContext, CancellationToken cancellationToken)
    {
        var tenantId = caller.TenantId!.Value;
        var user = await dbContext.UsersInTenant(tenantId).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (CheckTarget(caller, user, tenantId) is { } protectedMember)
        {
            return protectedMember;
        }

        user.RevokeMemberships(tenantId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static IResult? CheckAssignment(ITenantContext caller, string role, Guid? branchId)
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

    private static IResult? CheckTarget(ITenantContext caller, UserAccount user, Guid tenantId)
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

    private static async Task<string> TenantNameAsync(IdentityDbContext dbContext, ITenantContext caller, CancellationToken cancellationToken)
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
}
