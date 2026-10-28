using Microsoft.EntityFrameworkCore;
using RestaurantAI.Restaurant.Api.Persistence;
using RestaurantAI.Restaurant.Domain;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Restaurant.Api.Endpoints;

public static class RestaurantEndpoints
{
    public sealed record RegisterRestaurantRequest(string Name, string DefaultCurrency, string TimeZone);

    public sealed record UpdateProfileRequest(string Description, IReadOnlyList<string> Cuisines, IReadOnlyList<string> DietaryOptions);

    public sealed record SetListingRequest(bool Listed);

    public sealed record AddBranchRequest(string Name, string Address);

    public sealed record OpeningPeriodDto(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

    public sealed record UpdateBranchRequest(string Name, string Address, double? Latitude, double? Longitude, IReadOnlyList<OpeningPeriodDto> OpeningHours);

    public sealed record BranchResponse(Guid Id, string Name, string Address, double? Latitude, double? Longitude, IReadOnlyList<OpeningPeriodDto> OpeningHours)
    {
        public static BranchResponse From(Branch b) =>
            new(b.Id, b.Name, b.Address, b.Latitude, b.Longitude, b.OpeningHours.Select(p => new OpeningPeriodDto(p.Day, p.Opens, p.Closes)).ToList());
    }

    public sealed record RestaurantResponse(
        Guid Id,
        string Name,
        string Description,
        IReadOnlyList<string> Cuisines,
        IReadOnlyList<string> DietaryOptions,
        string DefaultCurrency,
        string TimeZone,
        string Status,
        bool IsListed,
        IReadOnlyList<BranchResponse> Branches)
    {
        public static RestaurantResponse From(Domain.Restaurant r) => new(
            r.Id, r.Name, r.Description, r.Cuisines, r.DietaryOptions, r.DefaultCurrency, r.TimeZone, r.Status.ToString(), r.IsListed,
            r.Branches.Select(BranchResponse.From).ToList());
    }

    public static IEndpointRouteBuilder MapRestaurantEndpoints(this IEndpointRouteBuilder app)
    {
        var restaurants = app.MapGroup("/api/restaurants").WithTags("Restaurants");

        // Platform onboarding creates the tenant; the resulting id becomes the tenant id.
        restaurants.MapPost("/", RegisterAsync).RequireAuthorization(policy => policy.RequireRole(PlatformRoles.PlatformAdministrator));
        restaurants.MapGet("/current", GetCurrentAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapPut("/current/profile", UpdateProfileAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapPut("/current/listing", SetListingAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapPost("/current/branches", AddBranchAsync).RequirePermission(PlatformPermissions.RestaurantManage);
        restaurants.MapPut("/current/branches/{branchId:guid}", UpdateBranchAsync).RequirePermission(PlatformPermissions.RestaurantManage);

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegisterRestaurantRequest request, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.DefaultCurrency.Length != 3)
        {
            return TypedResults.Problem(title: "restaurant.invalid", detail: "Name and a 3-letter currency are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var restaurant = Domain.Restaurant.Register(request.Name.Trim(), request.DefaultCurrency.ToUpperInvariant(), request.TimeZone);
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/restaurants/{restaurant.Id}", RestaurantResponse.From(restaurant));
    }

    private static async Task<IResult> GetCurrentAsync(ITenantContext tenant, RestaurantDbContext dbContext, CancellationToken cancellationToken)
    {
        var restaurant = await dbContext.Restaurants.AsNoTracking().Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        return restaurant is null ? TypedResults.NotFound() : TypedResults.Ok(RestaurantResponse.From(restaurant));
    }

    private static Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            restaurant.UpdateProfile(request.Description, request.Cuisines, request.DietaryOptions);
            return TypedResults.Ok(RestaurantResponse.From(restaurant));
        });

    private static Task<IResult> SetListingAsync(SetListingRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            if (request.Listed)
            {
                restaurant.Activate();
            }
            else
            {
                restaurant.Unlist();
            }

            return TypedResults.Ok(RestaurantResponse.From(restaurant));
        });

    private static Task<IResult> AddBranchAsync(AddBranchRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            var branch = restaurant.AddBranch(request.Name, request.Address);
            return TypedResults.Created($"/api/restaurants/current/branches/{branch.Id}", BranchResponse.From(branch));
        });

    private static Task<IResult> UpdateBranchAsync(Guid branchId, UpdateBranchRequest request, ITenantContext tenant, RestaurantDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        MutateAsync(tenant, dbContext, timeProvider, cancellationToken, restaurant =>
        {
            var branch = restaurant.GetBranch(branchId);
            branch.UpdateDetails(request.Name, request.Address, request.Latitude, request.Longitude);
            branch.SetOpeningHours(request.OpeningHours.Select(p => new OpeningPeriod(p.Day, p.Opens, p.Closes)));
            return TypedResults.Ok(BranchResponse.From(branch));
        });

    /// <summary>Loads the caller's restaurant, applies a change and commits it with the discovery read model.</summary>
    private static async Task<IResult> MutateAsync(
        ITenantContext tenant,
        RestaurantDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        Func<Domain.Restaurant, IResult> change)
    {
        var restaurant = await dbContext.Restaurants.Include(r => r.Branches)
            .SingleOrDefaultAsync(r => r.Id == tenant.TenantId, cancellationToken);
        if (restaurant is null)
        {
            return TypedResults.NotFound();
        }

        var result = change(restaurant);
        await dbContext.SyncDiscoveryListingsAsync(restaurant, timeProvider, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}
