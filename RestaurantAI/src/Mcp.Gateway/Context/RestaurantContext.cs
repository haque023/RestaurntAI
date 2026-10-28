namespace RestaurantAI.Mcp.Gateway.Context;

/// <summary>The tenant (restaurant) the tool call is scoped to, taken from the validated token.</summary>
public sealed record RestaurantContext(Guid TenantId);
