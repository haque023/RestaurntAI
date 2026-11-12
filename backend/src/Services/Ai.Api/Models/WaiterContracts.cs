namespace RestaurantAI.AI.Api.Models;

public sealed record WaiterTurnRequest(Guid? ConversationId, Guid BranchId, Guid? TableId, string Message);

public sealed record WaiterTurnResponse(Guid ConversationId, Guid TurnId, string Reply, string Outcome);
