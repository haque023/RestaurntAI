using Microsoft.EntityFrameworkCore;
using RestaurantAI.Identity.Api.Persistence;
using RestaurantAI.Identity.Api.Security;
using RestaurantAI.Identity.Domain;
using RestaurantAI.SharedKernel.Tenancy;
using static RestaurantAI.Identity.Api.Endpoints.IdentityEndpoints;

namespace RestaurantAI.Identity.Api.Endpoints;

/// <summary>Platform-administrator user management (separate privileged policy).</summary>
public static class AdminIdentityEndpoints
{
    public sealed record AdminUserDto(
        Guid Id,
        string Email,
        string DisplayName,
        bool IsPlatformAdministrator,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt,
        IReadOnlyList<MembershipDto> Memberships);

    public sealed record CreateOwnerRequest(Guid TenantId, string TenantName, string Email, string? DisplayName, string? Password);

    public static IEndpointRouteBuilder MapAdminIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/identity")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(PlatformRoles.PlatformAdministrator));

        admin.MapGet("/users", ListUsersAsync);
        admin.MapGet("/tenants/{tenantId:guid}/members", ListTenantMembersAsync);
        admin.MapPost("/owners", CreateOwnerAsync);
        admin.MapPost("/users/{userId:guid}/deactivate", (Guid userId, ITenantContext caller, IdentityDbContext db, CancellationToken ct) =>
            SetActiveAsync(userId, false, caller, db, ct));
        admin.MapPost("/users/{userId:guid}/activate", (Guid userId, ITenantContext caller, IdentityDbContext db, CancellationToken ct) =>
            SetActiveAsync(userId, true, caller, db, ct));

        return app;
    }

    private static async Task<IResult> ListUsersAsync(string? search, int? take, IdentityDbContext dbContext, CancellationToken cancellationToken)
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

    private static async Task<IResult> ListTenantMembersAsync(Guid tenantId, IdentityDbContext dbContext, CancellationToken cancellationToken)
    {
        var users = await dbContext.UsersInTenant(tenantId).AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);
        return TypedResults.Ok(users.Select(u => StaffEndpoints.ToDto(u, tenantId)).ToList());
    }

    /// <summary>Creates (or links) the owner account for a restaurant the administrator onboarded.</summary>
    private static async Task<IResult> CreateOwnerAsync(
        CreateOwnerRequest request,
        IdentityDbContext dbContext,
        PasswordHasher hasher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
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

    private static async Task<IResult> SetActiveAsync(Guid userId, bool active, ITenantContext caller, IdentityDbContext dbContext, CancellationToken cancellationToken)
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
}
