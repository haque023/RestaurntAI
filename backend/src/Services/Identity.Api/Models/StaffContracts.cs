namespace RestaurantAI.Identity.Api.Models;

public sealed record StaffMemberDto(Guid UserId, string Email, string DisplayName, bool IsActive, DateTimeOffset? LastLoginAt, IReadOnlyList<MembershipDto> Roles);

public sealed record AddStaffRequest(string Email, string? DisplayName, string? Password, string Role, Guid? BranchId);

public sealed record UpdateStaffRequest(string Role, Guid? BranchId);
