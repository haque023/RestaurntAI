namespace RestaurantAI.Consumer.Api.Models;

/// <summary>A restaurant a consumer saved to "My restaurants". Platform-wide, not tenant-scoped.</summary>
public sealed class FavoriteRestaurant
{
    public const int MaxPerUser = 500;

    private FavoriteRestaurant()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>The restaurant (tenant) id.</summary>
    public Guid RestaurantId { get; private set; }

    public DateTimeOffset SavedAt { get; private set; }

    public static FavoriteRestaurant Create(Guid userId, Guid restaurantId, DateTimeOffset now) =>
        new() { UserId = userId, RestaurantId = restaurantId, SavedAt = now };
}
