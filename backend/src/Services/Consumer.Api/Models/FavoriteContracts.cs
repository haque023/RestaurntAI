namespace RestaurantAI.Consumer.Api.Models;

public sealed record FavoriteResponse(Guid RestaurantId, DateTimeOffset SavedAt);
