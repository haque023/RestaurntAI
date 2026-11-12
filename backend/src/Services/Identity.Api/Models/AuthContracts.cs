namespace RestaurantAI.Identity.Api.Models;

/// <summary>Which app is signing in; decides the shape of the issued token.</summary>
public enum SessionScope
{
    Customer,
    Staff,
    Admin,
}

public sealed record DevTokenRequest(
    Guid? UserId,
    Guid? TenantId,
    Guid? BranchId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    int? LifetimeMinutes);

public sealed record RegisterRequest(string Email, string Password, string? DisplayName);

public sealed record LoginRequest(string Email, string Password, SessionScope? Scope, Guid? TenantId);

public sealed record SwitchTenantRequest(Guid TenantId);

public sealed record VenueSessionRequest(Guid? TenantId, Guid? BranchId, string? QrToken);

public sealed record RestaurantSignupRequest(
    string Email,
    string Password,
    string? DisplayName,
    string RestaurantName,
    string Currency,
    string TimeZone);

public sealed record SessionUserDto(Guid Id, string? Email, string? DisplayName, bool IsPlatformAdministrator, bool IsGuest);

public sealed record MembershipDto(Guid TenantId, string TenantName, Guid? BranchId, string Role);

public sealed record VenueDto(Guid TenantId, Guid? BranchId, Guid? TableId, string RestaurantName, string? BranchName, string? TableLabel);

public sealed record SessionResponse(
    string AccessToken,
    int ExpiresIn,
    SessionUserDto User,
    IReadOnlyList<MembershipDto> Memberships,
    VenueDto? Venue = null)
{
    public string TokenType => "Bearer";
}
