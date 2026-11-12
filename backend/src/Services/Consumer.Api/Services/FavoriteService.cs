using Microsoft.EntityFrameworkCore;
using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Consumer.Api.Services;

/// <summary>"My restaurants": favorites are keyed by the signed-in user only, across all restaurants.</summary>
public sealed class FavoriteService(ITenantContext caller, ConsumerDbContext dbContext, TimeProvider timeProvider) : IFavoriteService
{
    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        if (caller.UserId is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var favorites = await dbContext.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.SavedAt)
            .Select(f => new FavoriteResponse(f.RestaurantId, f.SavedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(favorites);
    }

    /// <summary>Idempotent: saving an already saved restaurant returns the existing favorite.</summary>
    public async Task<IResult> AddAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        if (caller.UserId is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var existing = await dbContext.Favorites.AsNoTracking().SingleOrDefaultAsync(f => f.UserId == userId && f.RestaurantId == restaurantId, cancellationToken);
        if (existing is not null)
        {
            return TypedResults.Ok(new FavoriteResponse(existing.RestaurantId, existing.SavedAt));
        }

        if (await dbContext.Favorites.CountAsync(f => f.UserId == userId, cancellationToken) >= FavoriteRestaurant.MaxPerUser)
        {
            return TypedResults.Problem(title: "favorites.limit", detail: $"You can save up to {FavoriteRestaurant.MaxPerUser} restaurants.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var favorite = FavoriteRestaurant.Create(userId, restaurantId, timeProvider.GetUtcNow());
        dbContext.Favorites.Add(favorite);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Saved concurrently from another tab: already a favorite.
        }

        return TypedResults.Ok(new FavoriteResponse(favorite.RestaurantId, favorite.SavedAt));
    }

    public async Task<IResult> RemoveAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        if (caller.UserId is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        await dbContext.Favorites.Where(f => f.UserId == userId && f.RestaurantId == restaurantId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
