using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantAI.Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Identity")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public IResult Me() =>
        service.Me(User);

    [HttpPost("register")]
    [AllowAnonymous]
    public Task<IResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        service.RegisterAsync(request, cancellationToken);

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<IResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        service.LoginAsync(request, cancellationToken);

    [HttpPost("switch")]
    [Authorize]
    public Task<IResult> SwitchTenantAsync(SwitchTenantRequest request, CancellationToken cancellationToken) =>
        service.SwitchTenantAsync(request, cancellationToken);

    [HttpGet("memberships")]
    [Authorize]
    public Task<IResult> GetMembershipsAsync(CancellationToken cancellationToken) =>
        service.GetMembershipsAsync(cancellationToken);

    [HttpPost("venue-session")]
    [AllowAnonymous]
    public Task<IResult> VenueSessionAsync(VenueSessionRequest request, CancellationToken cancellationToken) =>
        service.VenueSessionAsync(request, User, cancellationToken);

    [HttpPost("signup/restaurant")]
    [AllowAnonymous]
    public Task<IResult> SignupRestaurantAsync(RestaurantSignupRequest request, CancellationToken cancellationToken) =>
        service.SignupRestaurantAsync(request, cancellationToken);

    [HttpPost("dev-token")]
    [AllowAnonymous]
    public IResult IssueDevToken(DevTokenRequest request) =>
        service.IssueDevToken(request);
}
