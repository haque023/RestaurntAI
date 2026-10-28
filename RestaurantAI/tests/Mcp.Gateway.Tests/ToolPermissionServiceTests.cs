using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Context;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Mcp.Gateway.Tests;

public sealed class ToolPermissionServiceTests
{
    private static readonly Guid BranchA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid BranchB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly ToolPermissionService _service = new();

    private static ToolCallerContext Caller(Guid? boundBranch, params string[] permissions) => new(
        new UserContext(Guid.NewGuid(), new HashSet<string>(), permissions.ToHashSet(), IsPlatformAdministrator: false),
        new RestaurantContext(Guid.NewGuid()),
        new BranchContext(boundBranch, null),
        null,
        null);

    [Fact]
    public void Allows_a_tool_when_permission_and_branch_match() =>
        Assert.True(_service.Check(McpToolNames.CreateOrder, Caller(BranchA, PlatformPermissions.OrdersCreate), BranchA).Allowed);

    [Fact]
    public void Denies_a_tool_without_the_required_permission()
    {
        var result = _service.Check(McpToolNames.CreateOrder, Caller(BranchA, PlatformPermissions.MenuRead), BranchA);

        Assert.False(result.Allowed);
        Assert.Contains(PlatformPermissions.OrdersCreate, result.Reason);
    }

    [Fact]
    public void Denies_acting_on_another_branch_than_the_session_is_bound_to() =>
        Assert.False(_service.Check(McpToolNames.CreateOrder, Caller(BranchA, PlatformPermissions.OrdersCreate), BranchB).Allowed);

    [Fact]
    public void Denies_branch_scoped_tools_without_a_branch() =>
        Assert.False(_service.Check(McpToolNames.SearchMenu, Caller(null, PlatformPermissions.MenuRead), null).Allowed);

    [Fact]
    public void Denies_unknown_tools() =>
        Assert.False(_service.Check("drop_database", Caller(BranchA, PlatformPermissions.OrdersCreate), BranchA).Allowed);
}
