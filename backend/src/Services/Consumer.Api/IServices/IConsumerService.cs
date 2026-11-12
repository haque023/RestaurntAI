namespace RestaurantAI.Consumer.Api.IServices;

public interface IConsumerService
{
    Task<IResult> GetProfileAsync(CancellationToken cancellationToken);

    Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);
}
