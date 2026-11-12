namespace RestaurantAI.Mcp.Gateway.Context;

/// <summary>
/// The branch (and optional table, e.g. from a QR session) the caller is bound to.
/// A null branch means the caller is not branch-restricted (owners, platform admins, consumers browsing).
/// </summary>
public sealed record BranchContext(Guid? BranchId, Guid? TableId)
{
    public bool Allows(Guid branchId) => BranchId is null || BranchId == branchId;
}

/// <summary>The fully resolved caller for one tool execution.</summary>
public sealed record ToolCallerContext(UserContext User, RestaurantContext Restaurant, BranchContext Branch, string? ConversationId, string? TurnId);
