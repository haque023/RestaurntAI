namespace RestaurantAI.Mcp.Gateway.Context;

/// <summary>The end user on whose behalf the AI agent is acting.</summary>
public sealed record UserContext(
    Guid UserId,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    bool IsPlatformAdministrator)
{
    public bool HasPermission(string permission) => IsPlatformAdministrator || Permissions.Contains(permission);
}
