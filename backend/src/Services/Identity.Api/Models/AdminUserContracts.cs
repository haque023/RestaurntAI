namespace RestaurantAI.Identity.Api.Models;

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
