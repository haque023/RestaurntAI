namespace RestaurantAI.Identity.Api.IServices;

public interface IAdminUserService
{
    Task<IResult> ListUsersAsync(string? search, int? take, CancellationToken cancellationToken);

    Task<IResult> ListTenantMembersAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IResult> CreateOwnerAsync(CreateOwnerRequest request, CancellationToken cancellationToken);

    Task<IResult> DeactivateAsync(Guid userId, CancellationToken cancellationToken);

    Task<IResult> ActivateAsync(Guid userId, CancellationToken cancellationToken);
}
