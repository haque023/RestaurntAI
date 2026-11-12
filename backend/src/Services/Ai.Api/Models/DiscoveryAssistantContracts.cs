namespace RestaurantAI.AI.Api.Models;

public sealed record DiscoveryTurnRequest(Guid? ConversationId, string Message, double? Latitude, double? Longitude);

public sealed record DiscoveryTurnResponse(Guid ConversationId, Guid TurnId, string Reply, string Outcome);
