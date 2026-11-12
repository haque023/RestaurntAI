namespace RestaurantAI.Restaurant.Api.IServices;

public interface ITableService
{
    Task<IResult> ListAsync(Guid branchId, CancellationToken cancellationToken);

    Task<IResult> CreateAsync(Guid branchId, TableRequest request, CancellationToken cancellationToken);

    Task<IResult> UpdateAsync(Guid tableId, TableRequest request, CancellationToken cancellationToken);

    Task<IResult> DeleteAsync(Guid tableId, CancellationToken cancellationToken);

    Task<IResult> RotateQrAsync(Guid tableId, CancellationToken cancellationToken);

    Task<IResult> SetStatusAsync(Guid tableId, TableStatusRequest request, CancellationToken cancellationToken);
}
