namespace RestaurantAI.Order.Api.IServices;

public interface IServiceRequestService
{
    Task<IResult> OpenAsync(OpenServiceRequest request, CancellationToken cancellationToken);

    Task<IResult> ListMineAsync(CancellationToken cancellationToken);

    Task<IResult> ListAsync(Guid? branchId, bool? open, CancellationToken cancellationToken);

    Task<IResult> AcknowledgeAsync(Guid requestId, CancellationToken cancellationToken);

    Task<IResult> ResolveAsync(Guid requestId, CancellationToken cancellationToken);
}
