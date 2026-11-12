namespace RestaurantAI.Restaurant.Api.IServices;

public interface IRestaurantService
{
    Task<IResult> RegisterAsync(RegisterRestaurantRequest request, CancellationToken cancellationToken);

    Task<IResult> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IResult> GetSummaryAsync(CancellationToken cancellationToken);

    Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<IResult> SetListingAsync(SetListingRequest request, CancellationToken cancellationToken);

    Task<IResult> AddBranchAsync(AddBranchRequest request, CancellationToken cancellationToken);

    Task<IResult> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken);

    Task<IResult> GetAiSettingsAsync(CancellationToken cancellationToken);

    Task<IResult> UpdateAiSettingsAsync(AiSettingsRequest request, CancellationToken cancellationToken);

    IResult GetPlans();

    Task<IResult> GetAiContextAsync(CancellationToken cancellationToken);
}
