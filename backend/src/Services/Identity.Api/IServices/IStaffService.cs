namespace RestaurantAI.Identity.Api.IServices;

public interface IStaffService
{
    Task<IResult> ListAsync(CancellationToken cancellationToken);

    IResult GetRoles();

    Task<IResult> AddAsync(AddStaffRequest request, CancellationToken cancellationToken);

    Task<IResult> UpdateAsync(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken);

    Task<IResult> RemoveAsync(Guid userId, CancellationToken cancellationToken);
}
