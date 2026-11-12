namespace RestaurantAI.Admin.Api.IServices;

public interface IAdminService
{
    Task<IResult> OverviewAsync(CancellationToken cancellationToken);

    Task<IResult> AuditAsync(string? type, string? module, Guid? tenantId, Guid? actorId, DateTimeOffset? from, DateTimeOffset? to, int? take, CancellationToken cancellationToken);
}
