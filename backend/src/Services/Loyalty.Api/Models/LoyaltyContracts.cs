namespace RestaurantAI.Loyalty.Api.Models;

public sealed record LoyaltyBalanceResponse(Guid AccountId, Guid CustomerId, int Balance);
