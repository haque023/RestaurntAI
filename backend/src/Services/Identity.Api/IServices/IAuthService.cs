using System.Security.Claims;

namespace RestaurantAI.Identity.Api.IServices;

public interface IAuthService
{
    IResult Me(ClaimsPrincipal user);

    Task<IResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<IResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<IResult> SwitchTenantAsync(SwitchTenantRequest request, CancellationToken cancellationToken);

    Task<IResult> GetMembershipsAsync(CancellationToken cancellationToken);

    Task<IResult> VenueSessionAsync(VenueSessionRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<IResult> SignupRestaurantAsync(RestaurantSignupRequest request, CancellationToken cancellationToken);

    IResult IssueDevToken(DevTokenRequest request);
}
