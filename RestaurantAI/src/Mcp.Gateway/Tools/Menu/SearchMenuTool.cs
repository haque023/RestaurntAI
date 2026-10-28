using System.ComponentModel;
using ModelContextProtocol.Server;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;

namespace RestaurantAI.Mcp.Gateway.Tools.Menu;

[McpServerToolType]
public sealed class SearchMenuTool(McpAuthorizationService authorization, McpAuditService audit, BusinessApiClient api)
{
    [McpServerTool(Name = McpToolNames.SearchMenu, ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Searches currently available menu items by text, maximum price and dietary tags (e.g. 'something spicy under 15').")]
    public Task<string> ExecuteAsync(
        [Description("Free-text search over name, description and category.")] string? query = null,
        [Description("Maximum unit price in the branch currency.")] decimal? maxPrice = null,
        [Description("Dietary tags that must all match, e.g. ['vegan','gluten-free'].")] string[]? dietaryTags = null,
        [Description("Branch id. Optional when the session is bound to a branch.")] Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var caller = authorization.Authorize(McpToolNames.SearchMenu, branchId);
        var branch = branchId ?? caller.Branch.BranchId!.Value;

        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            parameters.Add($"q={Uri.EscapeDataString(query)}");
        }

        if (maxPrice is { } price)
        {
            parameters.Add($"maxPrice={price.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (dietaryTags is { Length: > 0 })
        {
            parameters.Add($"dietary={Uri.EscapeDataString(string.Join(',', dietaryTags))}");
        }

        var path = $"/api/menu/branches/{branch}/items/search" + (parameters.Count > 0 ? "?" + string.Join('&', parameters) : string.Empty);
        return audit.TrackAsync(McpToolNames.SearchMenu, caller, () => api.GetAsync(path, cancellationToken));
    }
}
